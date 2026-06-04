using Microsoft.Extensions.Options;
using System.Collections;
using System.Globalization;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using TwinCAT.Ads;

namespace TwincatMcpServer.TwinCat;

internal sealed class TwinCatAutomationService : IDisposable
{
    private const int RuntimeStateVerificationTimeoutMilliseconds = 45_000;
    private const int RuntimeStatePollMilliseconds = 500;
    private const int XaeCommandRetryTimeoutMilliseconds = 30_000;
    private const int XaeCommandRetryDelayMilliseconds = 100;
    private const int XaeCommandDialogPollMilliseconds = 50;
    private const int XaeModalDialogHandleLimit = 10;
    private const int RpcCallRejected = unchecked((int)0x80010001);
    private const int RpcServerCallRetryLater = unchecked((int)0x8001010A);

    private readonly TwinCatAutomationOptions _options;
    private readonly StaComDispatcher _dispatcher = new();

    private object? _dte;
    private object? _activeProject;
    private object? _sysManager;
    private string? _openSolutionPath;

    public TwinCatAutomationService(IOptions<TwinCatAutomationOptions> options)
    {
        _options = options.Value;
    }

    public Task<object> GetConfigAsync()
    {
        return Task.FromResult<object>(new
        {
            _options.XaeProgId,
            _options.Port,
            _options.BindAddress,
            configuredSolutionPath = _options.TwinCatSolutionPath,
            configuredTwinSafeLoaderPath = _options.TwinSafeLoaderPath,
            _options.ProjectLoadTimeoutSeconds
        });
    }

    public Task<object> GetStatusAsync()
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            TryHandleModalDialog();

            if (_sysManager is null)
            {
                TryRefreshActiveProject(projectIndex: null, projectName: null, TimeSpan.Zero);
            }

            string? targetNetId = null;
            string? lastErrorMessages = null;

            if (_sysManager is not null)
            {
                targetNetId = TryInvokeString(_sysManager, "GetTargetNetId");
                lastErrorMessages = TryInvokeString(_sysManager, "GetLastErrorMessages");
            }

            return new
            {
                _options.XaeProgId,
                _options.Port,
                _options.BindAddress,
                configuredSolutionPath = _options.TwinCatSolutionPath,
                configuredTwinSafeLoaderPath = _options.TwinSafeLoaderPath,
                _options.ProjectLoadTimeoutSeconds,
                xaeRunning = ActiveComObject.IsRunning(_options.XaeProgId),
                connected = _dte is not null,
                solutionOpen = IsSolutionOpen(),
                solutionPath = GetSolutionPath(),
                activeProject = GetActiveProjectName(),
                sysManagerReady = _sysManager is not null,
                targetNetId,
                lastErrorMessages
            };
        });
    }

    public Task<object> AttachOrLaunchAsync(bool showWindow, string? solutionPath, int? projectIndex, string? projectName)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            AttachOrLaunchCore(showWindow);

            if (!string.IsNullOrWhiteSpace(solutionPath))
            {
                OpenSolutionCore(solutionPath, projectIndex, projectName);
            }
            else
            {
                TryRefreshActiveProject(projectIndex, projectName);
            }

            return StatusSnapshot();
        });
    }

    public Task<object> OpenSolutionAsync(string? path, bool showWindow, int? projectIndex, string? projectName)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            AttachOrLaunchCore(showWindow);
            OpenSolutionCore(path, projectIndex, projectName);
            return StatusSnapshot();
        });
    }

    public Task<object> SetActiveProjectAsync(int? projectIndex, string? projectName)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            EnsureDte();
            SetActiveProjectCore(projectIndex, projectName);
            return StatusSnapshot();
        });
    }

    public Task<object> SaveSolutionAsync()
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            SaveSolutionCore();
            return StatusSnapshot();
        });
    }

    public Task<object> CloseSolutionAsync(bool saveFirst)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            EnsureDte();

            if (!IsSolutionOpen())
            {
                throw new InvalidOperationException("No XAE solution is currently open.");
            }

            if (saveFirst)
            {
                SaveSolutionCore();
            }

            object solution = GetProperty(EnsureDte(), "Solution");
            InvokeMethod(solution, "Close", saveFirst);
            ReleaseProjectReferences();
            _openSolutionPath = null;

            return StatusSnapshot();
        });
    }

    public Task<object> QuitAsync(bool saveAll)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            if (_dte is null)
            {
                return StatusSnapshot();
            }

            if (saveAll && IsSolutionOpen())
            {
                SaveSolutionCore();
            }

            InvokeMethod(_dte, "Quit");
            ReleaseProjectReferences();
            ActiveComObject.Release(_dte);
            _dte = null;

            return StatusSnapshot();
        });
    }

    public Task<object> BuildSolutionAsync(bool waitForBuildToFinish, string? configuration)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            EnsureDte();

            object solutionBuild = GetProperty(GetProperty(_dte!, "Solution"), "SolutionBuild");

            if (!string.IsNullOrWhiteSpace(configuration))
            {
                object configs = GetProperty(solutionBuild, "SolutionConfigurations");
                object config = InvokeMethod(configs, "Item", configuration);
                InvokeMethod(config, "Activate");
            }

            InvokeMethod(solutionBuild, "Build", waitForBuildToFinish);

            return new
            {
                waitForBuildToFinish,
                failedProjects = TryGetInt(solutionBuild, "LastBuildInfo"),
                solutionPath = GetSolutionPath()
            };
        });
    }

    public Task<object> GetTargetNetIdAsync()
    {
        return _dispatcher.InvokeAsync<object>(() => new
        {
            targetNetId = InvokeMethod(EnsureSysManager(), "GetTargetNetId")?.ToString()
        });
    }

    public Task<object> SetTargetNetIdAsync(string netId)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            InvokeMethod(EnsureSysManager(), "SetTargetNetId", netId);
            return new { targetNetId = InvokeMethod(EnsureSysManager(), "GetTargetNetId")?.ToString() };
        });
    }

    public Task<object> ActivateConfigurationAsync(bool confirm)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            TwinCatSafety.RequireConfirmation(confirm, "activate the TwinCAT configuration");

            InvokeMethod(EnsureSysManager(), "ActivateConfiguration");
            return new
            {
                activated = true,
                lastErrorMessages = TryInvokeString(EnsureSysManager(), "GetLastErrorMessages")
            };
        });
    }

    public Task<object> RestartTwinCatAsync(bool confirm)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            TwinCatSafety.RequireConfirmation(confirm, "restart TwinCAT and switch the runtime to Run mode");

            InvokeMethod(EnsureSysManager(), "StartRestartTwinCAT");
            return new
            {
                restarted = true,
                lastErrorMessages = TryInvokeString(EnsureSysManager(), "GetLastErrorMessages")
            };
        });
    }

    public Task<object> SetRuntimeStateAsync(string state, bool confirm)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            TwinCatRuntimeState requestedState = TwinCatRuntimeStateParser.Parse(state);
            TwinCatSafety.RequireConfirmation(confirm, $"switch TwinCAT runtime state to {requestedState}");

            object sysManager = EnsureSysManager();
            string targetNetId = InvokeMethod(sysManager, "GetTargetNetId")?.ToString()
                ?? throw new InvalidOperationException("The active TwinCAT target AMS Net ID could not be read.");

            if (string.IsNullOrWhiteSpace(targetNetId))
            {
                throw new InvalidOperationException("The active TwinCAT target AMS Net ID is empty.");
            }

            TwinCatRuntimeSwitchDirection direction = requestedState == TwinCatRuntimeState.Config
                ? TwinCatRuntimeSwitchDirection.ToConfig
                : TwinCatRuntimeSwitchDirection.ToRun;

            if (requestedState == TwinCatRuntimeState.Run)
            {
                InvokeXaeCommandWithDialogRetry(
                    () => InvokeMethod(sysManager, "StartRestartTwinCAT"),
                    direction,
                    "restart TwinCAT and switch the runtime to Run mode");
            }
            else
            {
                InvokeXaeCommandWithDialogRetry(
                    () => InvokeMethod(EnsureDte(), "ExecuteCommand", "TwinCAT.RestartTwinCATConfigMode", string.Empty),
                    direction,
                    "restart TwinCAT in Config mode");
            }

            RuntimeStateVerification verification = WaitForRuntimeState(targetNetId, requestedState, direction);
            string lastErrorMessages = TryInvokeString(sysManager, "GetLastErrorMessages") ?? string.Empty;

            return new
            {
                requestedState = requestedState.ToString(),
                verifiedState = verification.VerifiedState,
                targetNetId,
                success = verification.Success,
                lastErrorMessages,
                message = verification.Message
            };
        });
    }

    public Task<object> GetLastErrorMessagesAsync()
    {
        return _dispatcher.InvokeAsync<object>(() => new
        {
            lastErrorMessages = TryInvokeString(EnsureSysManager(), "GetLastErrorMessages") ?? string.Empty
        });
    }

    public Task<object> LookupTreeItemAsync(string path, bool includeChildren, bool includeXml, bool recursiveXml, int maxChildren)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            object item = LookupTreeItem(path);
            return new
            {
                item = TreeItemSummary.From(item, includeChildren, Math.Clamp(maxChildren, 0, 500)),
                xml = includeXml ? InvokeMethod(item, "ProduceXml", recursiveXml)?.ToString() : null
            };
        });
    }

    public Task<object> ProduceXmlAsync(string path, bool recursive)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            object item = LookupTreeItem(path);
            return new
            {
                path,
                recursive,
                xml = InvokeMethod(item, "ProduceXml", recursive)?.ToString()
            };
        });
    }

    public Task<object> ImportSafetyProjectAsync(string projectPath, string? projectName, string importMode)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            TwinSafeProjectImportMode mode = TwinSafeProjectImportModeParser.Parse(importMode);
            string fullProjectPath = ResolveSafetyProjectPath(projectPath);
            string childName = ResolveSafetyProjectName(projectName, mode);
            object safety = LookupTreeItem("TISC");
            object imported = InvokeMethod(safety, "CreateChild", childName, mode.Subtype, string.Empty, fullProjectPath);

            return new
            {
                projectPath = fullProjectPath,
                projectName = childName,
                importMode = mode.Name,
                imported = TreeItemSummary.From(imported, includeChildren: false, maxChildren: 0)
            };
        });
    }

    public Task<object> ConsumeXmlAsync(string path, string xml, bool confirm)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            TwinCatSafety.RequireConfirmation(confirm, "import XML into the TwinCAT project");
            object item = LookupTreeItem(path);
            InvokeMethod(item, "ConsumeXml", xml);
            return new
            {
                path,
                consumed = true,
                item = TreeItemSummary.From(item, includeChildren: false, maxChildren: 0)
            };
        });
    }

    public Task<object> CreateChildAsync(string parentPath, string name, int subtype, string? before, string? info)
    {
        return _dispatcher.InvokeAsync<object>(() =>
        {
            object parent = LookupTreeItem(parentPath);
            object child = InvokeMethod(parent, "CreateChild", name, subtype, before ?? string.Empty, info);
            return new
            {
                parentPath,
                created = TreeItemSummary.From(child, includeChildren: false, maxChildren: 0)
            };
        });
    }

    public void Dispose()
    {
        _dispatcher.InvokeAsync(() =>
        {
            ReleaseProjectReferences();
            ActiveComObject.Release(_dte);
            _dte = null;
        }).GetAwaiter().GetResult();
        _dispatcher.Dispose();
    }

    private void AttachOrLaunchCore(bool showWindow)
    {
        TryHandleModalDialog();

        if (_dte is null)
        {
            if (ActiveComObject.IsRunning(_options.XaeProgId))
            {
                _dte = ActiveComObject.GetInstance(_options.XaeProgId);
            }
            else
            {
                Type? dteType = Type.GetTypeFromProgID(_options.XaeProgId, throwOnError: false);
                if (dteType is null)
                {
                    throw new InvalidOperationException(
                        $"COM ProgID '{_options.XaeProgId}' is not registered. Install TwinCAT XAE or set McpConfig:XaeProgId.");
                }

                _dte = Activator.CreateInstance(dteType)
                    ?? throw new InvalidOperationException($"Failed to create '{_options.XaeProgId}'.");
            }
        }

        TryHandleModalDialog();
        TrySetProperty(_dte, "UserControl", true);
        TrySetMainWindowVisible(showWindow);
    }

    private void TrySetMainWindowVisible(bool visible)
    {
        if (_dte is null)
        {
            return;
        }

        try
        {
            object mainWindow = GetProperty(_dte, "MainWindow");
            TrySetProperty(mainWindow, "Visible", visible);
        }
        catch (COMException)
        {
        }
        catch (TargetInvocationException)
        {
        }
    }

    private void OpenSolutionCore(string? path, int? projectIndex, string? projectName)
    {
        string solutionPath = ResolveSolutionPath(path);

        if (!File.Exists(solutionPath))
        {
            throw new FileNotFoundException($"Solution file not found: {solutionPath}", solutionPath);
        }

        if (!string.Equals(Path.GetExtension(solutionPath), ".sln", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"XAE automation opens Visual Studio solution files. Expected .sln, got: {solutionPath}");
        }

        TryHandleModalDialog();
        object solution = GetProperty(EnsureDte(), "Solution");
        InvokeMethod(solution, "Open", solutionPath);
        TryHandleModalDialog();
        _openSolutionPath = solutionPath;
        SetActiveProjectCore(projectIndex, projectName);
    }

    private string ResolveSolutionPath(string? path)
    {
        string? configuredPath = FirstNotBlank(
            path,
            _options.TwinCatSolutionPath);

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                "No solution path was supplied. Pass path to xae_open_solution or set McpConfig:TwinCatSolutionPath.");
        }

        string fullPath = Path.GetFullPath(configuredPath);
        return fullPath;
    }

    private void SetActiveProjectCore(int? projectIndex, string? projectName)
    {
        (object project, object sysManager) = WaitForProject(projectIndex, projectName, GetProjectLoadTimeout());

        ActiveComObject.Release(_sysManager);
        ActiveComObject.Release(_activeProject);
        _activeProject = project;
        _sysManager = sysManager;
    }

    private (object Project, object SysManager) WaitForProject(int? projectIndex, string? projectName, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        Exception? lastError = null;

        while (true)
        {
            TryHandleModalDialog();

            try
            {
                object projects = GetProjects();
                int twinCatProjectIndex = 0;

                foreach (object project in EnumerateProjects(projects))
                {
                    string? name = TryGetString(project, "Name");
                    if (!string.IsNullOrWhiteSpace(projectName) &&
                        !string.Equals(name, projectName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!TryGetSysManager(project, out object? sysManager, out Exception? error))
                    {
                        lastError = error;
                        continue;
                    }

                    twinCatProjectIndex++;

                    if (projectIndex.HasValue && twinCatProjectIndex != projectIndex.Value)
                    {
                        continue;
                    }

                    return (project, sysManager);
                }
            }
            catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException or InvalidOperationException)
            {
                lastError = ex;
            }

            if (DateTime.UtcNow >= deadline)
            {
                break;
            }

            Thread.Sleep(200);
        }

        string target = ResolveProjectTargetDescription(projectIndex, projectName);
        throw new TimeoutException($"Timed out waiting for TwinCAT project {target} to load.", lastError);
    }

    private void TryRefreshActiveProject(int? projectIndex, string? projectName)
    {
        TryRefreshActiveProject(projectIndex, projectName, GetProjectLoadTimeout());
    }

    private void TryRefreshActiveProject(int? projectIndex, string? projectName, TimeSpan timeout)
    {
        if (_dte is null || !IsSolutionOpen())
        {
            return;
        }

        try
        {
            (object project, object sysManager) = WaitForProject(projectIndex, projectName, timeout);

            ActiveComObject.Release(_sysManager);
            ActiveComObject.Release(_activeProject);
            _activeProject = project;
            _sysManager = sysManager;
        }
        catch (Exception ex) when (ex is TimeoutException or COMException or TargetInvocationException or MissingMethodException or InvalidOperationException)
        {
        }
    }

    private object LookupTreeItem(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Tree item path is required.", nameof(path));
        }

        return InvokeMethod(EnsureSysManager(), "LookupTreeItem", path);
    }

    private void SaveSolutionCore()
    {
        EnsureDte();

        if (!IsSolutionOpen())
        {
            throw new InvalidOperationException("No XAE solution is currently open.");
        }

        if (_activeProject is not null)
        {
            InvokeMethod(_activeProject, "Save");
        }

        string? solutionPath = GetSolutionPath();
        if (!string.IsNullOrWhiteSpace(solutionPath))
        {
            InvokeMethod(GetProperty(_dte!, "Solution"), "SaveAs", solutionPath);
        }
    }

    private void InvokeXaeCommandWithDialogRetry(
        Action command,
        TwinCatRuntimeSwitchDirection direction,
        string operation)
    {
        ArgumentNullException.ThrowIfNull(command);

        DateTime deadline = DateTime.UtcNow.AddMilliseconds(XaeCommandRetryTimeoutMilliseconds);
        Exception? lastRejectedCall = null;

        while (DateTime.UtcNow < deadline)
        {
            XaeCommandAttemptResult attempt = RunXaeCommandAttempt(command, direction, deadline);
            if (attempt.Success)
            {
                return;
            }

            if (attempt.Exception is null)
            {
                break;
            }

            if (!IsRpcCallRejected(attempt.Exception))
            {
                throw new InvalidOperationException(
                    $"XAE failed while trying to {operation}.",
                    attempt.Exception);
            }

            lastRejectedCall = attempt.Exception;

            if (TryHandleModalDialog(direction))
            {
                continue;
            }

            Thread.Sleep(XaeCommandRetryDelayMilliseconds);
        }

        throw new InvalidOperationException(
            $"Timed out waiting for XAE to accept the command to {operation}. No supported modal dialog could be cleared.",
            lastRejectedCall);
    }

    private XaeCommandAttemptResult RunXaeCommandAttempt(
        Action command,
        TwinCatRuntimeSwitchDirection direction,
        DateTime deadline)
    {
        using ManualResetEventSlim commandCompleted = new(initialState: false);
        Exception? commandException = null;

        Thread commandThread = new(() =>
        {
            try
            {
                command();
            }
            catch (Exception ex)
            {
                commandException = ex;
            }
            finally
            {
                commandCompleted.Set();
            }
        })
        {
            IsBackground = true,
            Name = "TwinCAT XAE command"
        };

        commandThread.SetApartmentState(ApartmentState.STA);
        commandThread.Start();

        while (!commandCompleted.Wait(XaeCommandDialogPollMilliseconds))
        {
            TryHandleModalDialog(direction);

            if (DateTime.UtcNow >= deadline)
            {
                return new XaeCommandAttemptResult(Success: false, Exception: null);
            }
        }

        if (commandException is not null)
        {
            return new XaeCommandAttemptResult(Success: false, commandException);
        }

        return new XaeCommandAttemptResult(Success: true, Exception: null);
    }

    private RuntimeStateVerification WaitForRuntimeState(
        string targetNetId,
        TwinCatRuntimeState requestedState,
        TwinCatRuntimeSwitchDirection direction)
    {
        AdsState requestedAdsState = TwinCatRuntimeStateParser.ToAdsState(requestedState);
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(RuntimeStateVerificationTimeoutMilliseconds);
        RuntimeStateSnapshot? lastSnapshot = null;
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            TryHandleModalDialog(direction);

            try
            {
                lastSnapshot = ReadRuntimeState(targetNetId);
                if (lastSnapshot.AdsState == requestedAdsState)
                {
                    return new RuntimeStateVerification(
                        lastSnapshot.AdsState.ToString(),
                        Success: true,
                        Message: null);
                }
            }
            catch (Exception ex) when (ex is TwinCAT.AdsException or TimeoutException or IOException or SocketException)
            {
                lastError = ex;
            }

            Thread.Sleep(RuntimeStatePollMilliseconds);
        }

        string verifiedState = lastSnapshot?.AdsState.ToString() ?? "Unknown";
        string message = lastSnapshot is not null
            ? $"ADS verification timed out. Last ADS state was {lastSnapshot.AdsState} and device state was {lastSnapshot.DeviceState}."
            : $"ADS verification timed out before a state could be read. Last error: {lastError?.Message ?? "none"}.";

        return new RuntimeStateVerification(verifiedState, Success: false, message);
    }

    private static RuntimeStateSnapshot ReadRuntimeState(string targetNetId)
    {
        using AdsClient client = new();
        client.Connect(AmsNetId.Parse(targetNetId), AmsPort.SystemService);
        StateInfo state = client.ReadState();
        return new RuntimeStateSnapshot(state.AdsState, state.DeviceState);
    }

    private bool TryHandleModalDialog()
    {
        return TryHandleModalDialogs(TwinCatModalDialogPolicy.Decide);
    }

    private bool TryHandleModalDialog(TwinCatRuntimeSwitchDirection direction)
    {
        return TryHandleModalDialogs(dialog => TwinCatModalDialogPolicy.Decide(dialog, direction));
    }

    private bool TryHandleModalDialogs(Func<TwinCatModalDialog, TwinCatModalDialogDecision> decide)
    {
        bool handledAny = false;

        for (int attempt = 0; attempt < XaeModalDialogHandleLimit; attempt++)
        {
            TwinCatModalDialog? dialog = TwinCatModalDialogInspector.FindModalDialog(TryGetMainWindowHandle());
            if (dialog is null)
            {
                return handledAny;
            }

            TwinCatModalDialogDecision decision = decide(dialog);
            HandleModalDialogDecision(dialog, decision);
            handledAny = true;
        }

        return handledAny;
    }

    private static void HandleModalDialogDecision(
        TwinCatModalDialog dialog,
        TwinCatModalDialogDecision decision)
    {
        if (decision.ShouldConfirm)
        {
            if (!TwinCatModalDialogInspector.TryClickConfirmationButton(dialog, out _))
            {
                throw new InvalidOperationException(
                    $"XAE showed a recognized modal dialog, but no affirmative button could be clicked. {dialog.FormatForError()}");
            }
        }
        else if (decision.ShouldDecline)
        {
            if (!TwinCatModalDialogInspector.TryClickDeclineButton(dialog, out _))
            {
                throw new InvalidOperationException(
                    $"XAE showed a recognized modal dialog, but no negative button could be clicked. {dialog.FormatForError()}");
            }
        }

        if (decision.ShouldBlock)
        {
            throw new InvalidOperationException($"{decision.BlockReason} {dialog.FormatForError()}");
        }
    }

    private IntPtr TryGetMainWindowHandle()
    {
        if (_dte is null)
        {
            return IntPtr.Zero;
        }

        try
        {
            object mainWindow = GetProperty(_dte, "MainWindow");
            object handle = GetProperty(mainWindow, "HWnd");
            return new IntPtr(Convert.ToInt64(handle, CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException or FormatException)
        {
            return IntPtr.Zero;
        }
    }

    private object EnsureDte()
    {
        return _dte ?? throw new InvalidOperationException("XAE is not connected. Call xae_attach_or_launch or xae_open_solution first.");
    }

    private object EnsureSysManager()
    {
        if (_sysManager is null)
        {
            TryRefreshActiveProject(projectIndex: null, projectName: null);
        }

        if (_sysManager is null)
        {
            throw new InvalidOperationException("No TwinCAT project is active. Call xae_open_solution or xae_set_active_project first.");
        }

        return _sysManager;
    }

    private object GetProjects()
    {
        return GetProperty(GetProperty(EnsureDte(), "Solution"), "Projects");
    }

    private static IEnumerable<object> EnumerateProjects(object projects)
    {
        int count = Convert.ToInt32(GetProperty(projects, "Count"), CultureInfo.InvariantCulture);
        for (int index = 1; index <= count; index++)
        {
            object project = InvokeMethod(projects, "Item", index);
            yield return project;

            foreach (object childProject in EnumerateChildProjects(project))
            {
                yield return childProject;
            }
        }
    }

    private static IEnumerable<object> EnumerateChildProjects(object project)
    {
        object? projectItems = TryGetObject(project, "ProjectItems");
        if (projectItems is null)
        {
            yield break;
        }

        int count;
        try
        {
            count = Convert.ToInt32(GetProperty(projectItems, "Count"), CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException or FormatException)
        {
            yield break;
        }

        for (int index = 1; index <= count; index++)
        {
            object projectItem;
            try
            {
                projectItem = InvokeMethod(projectItems, "Item", index);
            }
            catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException)
            {
                continue;
            }

            object? childProject = TryGetObject(projectItem, "SubProject");
            if (childProject is null)
            {
                continue;
            }

            yield return childProject;

            foreach (object nestedProject in EnumerateChildProjects(childProject))
            {
                yield return nestedProject;
            }
        }
    }

    private bool IsSolutionOpen()
    {
        if (_dte is null)
        {
            return false;
        }

        try
        {
            object solution = GetProperty(_dte, "Solution");
            object isOpen = GetProperty(solution, "IsOpen");
            return Convert.ToBoolean(isOpen, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException)
        {
            return false;
        }
    }

    private string? GetSolutionPath()
    {
        if (_dte is null)
        {
            return _openSolutionPath;
        }

        try
        {
            string? fullName = TryGetString(GetProperty(_dte, "Solution"), "FullName");
            return string.IsNullOrWhiteSpace(fullName) ? _openSolutionPath : fullName;
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException)
        {
            return _openSolutionPath;
        }
    }

    private string? GetActiveProjectName()
    {
        return _activeProject is null ? null : TryGetString(_activeProject, "Name");
    }

    private object StatusSnapshot()
    {
        return new
        {
            connected = _dte is not null,
            solutionOpen = IsSolutionOpen(),
            solutionPath = GetSolutionPath(),
            activeProject = GetActiveProjectName(),
            sysManagerReady = _sysManager is not null,
            targetNetId = _sysManager is null ? null : TryInvokeString(_sysManager, "GetTargetNetId")
        };
    }

    private void ReleaseProjectReferences()
    {
        ActiveComObject.Release(_sysManager);
        ActiveComObject.Release(_activeProject);
        _sysManager = null;
        _activeProject = null;
    }

    private TimeSpan GetProjectLoadTimeout()
    {
        return TimeSpan.FromSeconds(Math.Max(1, _options.ProjectLoadTimeoutSeconds));
    }

    private static string ResolveProjectTargetDescription(int? projectIndex, string? projectName)
    {
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            return $"named '{projectName}'";
        }

        if (projectIndex.HasValue)
        {
            return $"index {projectIndex.Value}";
        }

        return "in the open solution";
    }

    private static bool TryGetSysManager(object project, out object sysManager, out Exception? error)
    {
        try
        {
            object candidate = GetProperty(project, "Object");
            if (!IsSysManagerObject(candidate))
            {
                sysManager = new { };
                error = new InvalidOperationException("Project.Object is not a TwinCAT system manager.");
                return false;
            }

            sysManager = candidate;
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException or InvalidOperationException)
        {
            sysManager = new { };
            error = ex;
            return false;
        }
    }

    private static bool IsSysManagerObject(object candidate)
    {
        try
        {
            InvokeMethod(candidate, "GetTargetNetId");
            return true;
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException or InvalidOperationException)
        {
            return false;
        }
    }

    private static object GetProperty(object target, string name)
    {
        return target.GetType().InvokeMember(
            name,
            BindingFlags.GetProperty,
            binder: null,
            target,
            args: null,
            culture: CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"COM property '{name}' returned null.");
    }

    private static bool TrySetProperty(object target, string name, object? value)
    {
        try
        {
            target.GetType().InvokeMember(
                name,
                BindingFlags.SetProperty,
                binder: null,
                target,
                args: new[] { value },
                culture: CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is COMException or MissingMethodException or TargetInvocationException)
        {
            return false;
        }
    }

    private static object InvokeMethod(object target, string name, params object?[] args)
    {
        try
        {
            return target.GetType().InvokeMember(
                name,
                BindingFlags.InvokeMethod,
                binder: null,
                target,
                args,
                culture: CultureInfo.InvariantCulture)
                ?? new { };
        }
        catch (MissingMethodException)
        {
            return target.GetType().InvokeMember(
                name,
                BindingFlags.GetProperty,
                binder: null,
                target,
                args,
                culture: CultureInfo.InvariantCulture)
                ?? new { };
        }
    }

    private static bool IsRpcCallRejected(Exception exception)
    {
        COMException? comException = UnwrapComException(exception);
        return comException is not null &&
            (comException.HResult == RpcCallRejected || comException.HResult == RpcServerCallRetryLater);
    }

    private static COMException? UnwrapComException(Exception exception)
    {
        Exception ex = exception;
        while (ex is TargetInvocationException { InnerException: not null } wrapper)
        {
            ex = wrapper.InnerException!;
        }

        return ex as COMException;
    }

    private static string? TryInvokeString(object target, string methodName)
    {
        try
        {
            return InvokeMethod(target, methodName)?.ToString();
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException)
        {
            return null;
        }
    }

    private static string? TryGetString(object target, string propertyName)
    {
        try
        {
            return GetProperty(target, propertyName)?.ToString();
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException)
        {
            return null;
        }
    }

    private static object? TryGetObject(object target, string propertyName)
    {
        try
        {
            return GetProperty(target, propertyName);
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException or InvalidOperationException)
        {
            return null;
        }
    }

    private static int? TryGetInt(object target, string propertyName)
    {
        try
        {
            return Convert.ToInt32(GetProperty(target, propertyName), CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException or FormatException)
        {
            return null;
        }
    }

    private static string? FirstNotBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string ResolveSafetyProjectPath(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new ArgumentException("Safety project path is required.", nameof(projectPath));
        }

        string fullPath = Path.GetFullPath(projectPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Safety project file not found: {fullPath}", fullPath);
        }

        string extension = Path.GetExtension(fullPath);
        if (!string.Equals(extension, ".splcproj", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".tfzip", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Expected a TwinSAFE safety project file (.splcproj) or archive (.tfzip), got: {fullPath}",
                nameof(projectPath));
        }

        return fullPath;
    }

    private static string ResolveSafetyProjectName(string? projectName, TwinSafeProjectImportMode mode)
    {
        if (mode == TwinSafeProjectImportMode.UseOriginalLocation)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(projectName))
        {
            throw new ArgumentException("Safety project name is required unless importMode is UseOriginalLocation.", nameof(projectName));
        }

        return projectName.Trim();
    }

    private sealed record TreeItemSummary(
        string? Name,
        string? PathName,
        int? ItemType,
        int? ChildCount,
        IReadOnlyList<TreeItemSummary>? Children)
    {
        public static TreeItemSummary From(object item, bool includeChildren, int maxChildren)
        {
            List<TreeItemSummary>? children = null;

            if (includeChildren && maxChildren > 0)
            {
                children = new List<TreeItemSummary>();

                try
                {
                    foreach (object child in (IEnumerable)item)
                    {
                        children.Add(From(child, includeChildren: false, maxChildren: 0));
                        if (children.Count >= maxChildren)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex) when (ex is COMException or InvalidCastException or TargetInvocationException)
                {
                    children = null;
                }
            }

            return new TreeItemSummary(
                TryGetString(item, "Name"),
                TryGetString(item, "PathName"),
                TryGetInt(item, "ItemType"),
                TryGetInt(item, "ChildCount"),
                children);
        }
    }

    private sealed record RuntimeStateSnapshot(AdsState AdsState, short DeviceState);

    private sealed record RuntimeStateVerification(string VerifiedState, bool Success, string? Message);

    private sealed record XaeCommandAttemptResult(bool Success, Exception? Exception);
}
