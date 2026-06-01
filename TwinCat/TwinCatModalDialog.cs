using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace TwincatMcpServer.TwinCat;

internal sealed record TwinCatModalDialog(
    IntPtr Handle,
    string Title,
    string Text,
    IReadOnlyList<TwinCatModalDialogButton> Buttons)
{
    public AutomationElement? Element { get; init; }

    public string FormatForError()
    {
        string title = string.IsNullOrWhiteSpace(Title) ? "(empty)" : Title;
        string text = string.IsNullOrWhiteSpace(Text) ? "(empty)" : Text;
        string buttons = Buttons.Count == 0
            ? "(none detected)"
            : string.Join(", ", Buttons.Select(button => $"'{button.Label}'"));

        return $"Dialog title: '{title}'. Dialog text: '{text}'. Available button labels: {buttons}.";
    }
}

internal sealed record TwinCatModalDialogButton(IntPtr Handle, string Label)
{
    public AutomationElement? Element { get; init; }
}

internal enum TwinCatRuntimeSwitchDirection
{
    ToConfig,
    ToRun
}

internal enum TwinCatModalDialogAction
{
    Confirm,
    Decline,
    DeclineAndBlock,
    Block
}

internal static class TwinCatModalDialogPolicy
{
    public static TwinCatModalDialogDecision Decide(
        TwinCatModalDialog dialog,
        TwinCatRuntimeSwitchDirection direction)
    {
        string content = $"{dialog.Title}\n{dialog.Text}";

        if (Contains(content, "Restart TwinCAT System in Config Mode"))
        {
            return direction == TwinCatRuntimeSwitchDirection.ToConfig
                ? TwinCatModalDialogDecision.Confirm
                : TwinCatModalDialogDecision.Block(
                    "XAE is asking to restart TwinCAT in Config mode while Run mode was requested.");
        }

        if (Contains(content, "Load I/O Devices"))
        {
            return TwinCatModalDialogDecision.Confirm;
        }

        if (Contains(content, "Activate Free Run"))
        {
            return direction == TwinCatRuntimeSwitchDirection.ToConfig
                ? TwinCatModalDialogDecision.Decline
                : TwinCatModalDialogDecision.DeclineAndBlock(
                    "XAE is asking to activate Free Run. This tool does not automatically confirm Free Run because it is not PLC Run.");
        }

        return TwinCatModalDialogDecision.Block("XAE is blocked by an unrecognized modal dialog.");
    }

    private static bool Contains(string text, string value)
    {
        return text.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record TwinCatModalDialogDecision(TwinCatModalDialogAction Action, string? BlockReason)
{
    public bool ShouldConfirm => Action == TwinCatModalDialogAction.Confirm;

    public bool ShouldDecline =>
        Action == TwinCatModalDialogAction.Decline ||
        Action == TwinCatModalDialogAction.DeclineAndBlock;

    public bool ShouldBlock =>
        Action == TwinCatModalDialogAction.Block ||
        Action == TwinCatModalDialogAction.DeclineAndBlock;

    public static TwinCatModalDialogDecision Confirm { get; } = new(TwinCatModalDialogAction.Confirm, BlockReason: null);

    public static TwinCatModalDialogDecision Decline { get; } = new(TwinCatModalDialogAction.Decline, BlockReason: null);

    public static TwinCatModalDialogDecision DeclineAndBlock(string reason)
    {
        return new TwinCatModalDialogDecision(TwinCatModalDialogAction.DeclineAndBlock, reason);
    }

    public static TwinCatModalDialogDecision Block(string reason)
    {
        return new TwinCatModalDialogDecision(TwinCatModalDialogAction.Block, reason);
    }
}

internal static class TwinCatModalDialogInspector
{
    private const int DialogDismissTimeoutMilliseconds = 1_000;

    public static TwinCatModalDialog? FindModalDialog(IntPtr mainWindowHandle)
    {
        List<TwinCatModalDialog> dialogs = [];
        HashSet<string> seenDialogKeys = [];

        foreach (AutomationElement root in GetCandidateRootElements(mainWindowHandle))
        {
            foreach (AutomationElement window in FindWindowElements(root))
            {
                string key = GetElementKey(window);
                if (!seenDialogKeys.Add(key))
                {
                    continue;
                }

                TwinCatModalDialog? dialog = TryCreateDialog(window);
                if (dialog is not null)
                {
                    dialogs.Add(dialog);
                }
            }
        }

        return dialogs
            .OrderByDescending(IsRecognizedDialog)
            .ThenBy(dialog => dialog.Buttons.Count == 0)
            .FirstOrDefault();
    }

    public static bool TryClickConfirmationButton(TwinCatModalDialog dialog, out string clickedLabel)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        return TryClickButton(dialog, IsAffirmativeButton, out clickedLabel);
    }

    public static bool TryClickDeclineButton(TwinCatModalDialog dialog, out string clickedLabel)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        return TryClickButton(dialog, IsNegativeButton, out clickedLabel);
    }

    private static IEnumerable<AutomationElement> GetCandidateRootElements(IntPtr mainWindowHandle)
    {
        HashSet<int> processIds = [];
        HashSet<string> seenRootKeys = [];

        if (mainWindowHandle != IntPtr.Zero)
        {
            AutomationElement? mainWindow = TryGetElementFromHandle(mainWindowHandle);
            if (mainWindow is not null)
            {
                processIds.Add(GetProcessId(mainWindow));

                if (seenRootKeys.Add(GetElementKey(mainWindow)))
                {
                    yield return mainWindow;
                }
            }
        }

        foreach (Process process in Process.GetProcessesByName("TcXaeShell"))
        {
            try
            {
                processIds.Add(process.Id);
            }
            finally
            {
                process.Dispose();
            }
        }

        if (processIds.Count == 0)
        {
            yield break;
        }

        foreach (AutomationElement window in FindAll(
            AutomationElement.RootElement,
            TreeScope.Children,
            Condition.TrueCondition))
        {
            if (!processIds.Contains(GetProcessId(window)))
            {
                continue;
            }

            string key = GetElementKey(window);
            if (seenRootKeys.Add(key))
            {
                yield return window;
            }
        }
    }

    private static IEnumerable<AutomationElement> FindWindowElements(AutomationElement root)
    {
        if (GetControlType(root) == ControlType.Window)
        {
            yield return root;
        }

        PropertyCondition windowCondition = new(AutomationElement.ControlTypeProperty, ControlType.Window);
        foreach (AutomationElement window in FindAll(root, TreeScope.Descendants, windowCondition))
        {
            yield return window;
        }
    }

    private static TwinCatModalDialog? TryCreateDialog(AutomationElement window)
    {
        string title = GetName(window);
        IReadOnlyList<TwinCatModalDialogButton> buttons = GetButtons(window);
        string text = GetChildText(window);

        if (LooksLikeMainWindow(title))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(text) && buttons.Count == 0)
        {
            return null;
        }

        if (buttons.Count == 0 && !IsRecognizedContent(title, text))
        {
            return null;
        }

        return new TwinCatModalDialog(GetNativeWindowHandle(window), title, text, buttons)
        {
            Element = window
        };
    }

    private static bool TryClickButton(
        TwinCatModalDialog dialog,
        Func<string, bool> buttonPredicate,
        out string clickedLabel)
    {
        foreach (TwinCatModalDialogButton button in dialog.Buttons)
        {
            string normalizedLabel = NormalizeButtonLabel(button.Label);
            if (!buttonPredicate(normalizedLabel))
            {
                continue;
            }

            if (TryInvokeButton(button) && WaitForDialogToClose(dialog))
            {
                clickedLabel = button.Label;
                return true;
            }

            clickedLabel = string.Empty;
            return false;
        }

        clickedLabel = string.Empty;
        return false;
    }

    private static IReadOnlyList<TwinCatModalDialogButton> GetButtons(AutomationElement dialog)
    {
        List<TwinCatModalDialogButton> buttons = [];
        PropertyCondition buttonCondition = new(AutomationElement.ControlTypeProperty, ControlType.Button);

        foreach (AutomationElement button in FindAll(dialog, TreeScope.Descendants, buttonCondition))
        {
            string label = GetName(button);
            if (!string.IsNullOrWhiteSpace(label))
            {
                buttons.Add(new TwinCatModalDialogButton(GetNativeWindowHandle(button), label)
                {
                    Element = button
                });
            }
        }

        return buttons;
    }

    private static string GetChildText(AutomationElement dialog)
    {
        List<string> textParts = [];
        PropertyCondition textCondition = new(AutomationElement.ControlTypeProperty, ControlType.Text);

        foreach (AutomationElement textElement in FindAll(dialog, TreeScope.Descendants, textCondition))
        {
            string text = GetName(textElement);
            if (!string.IsNullOrWhiteSpace(text))
            {
                textParts.Add(text.Trim());
            }
        }

        return string.Join(" ", textParts.Distinct(StringComparer.Ordinal));
    }

    private static AutomationElement? TryGetElementFromHandle(IntPtr handle)
    {
        try
        {
            return AutomationElement.FromHandle(handle);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static IEnumerable<AutomationElement> FindAll(
        AutomationElement root,
        TreeScope scope,
        Condition condition)
    {
        AutomationElementCollection elements;
        try
        {
            elements = root.FindAll(scope, condition);
        }
        catch (ElementNotAvailableException)
        {
            yield break;
        }
        catch (InvalidOperationException)
        {
            yield break;
        }
        catch (COMException)
        {
            yield break;
        }

        foreach (AutomationElement element in elements)
        {
            yield return element;
        }
    }

    private static string NormalizeButtonLabel(string label)
    {
        return label.Replace("&", string.Empty, StringComparison.Ordinal)
            .Replace("...", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static bool IsAffirmativeButton(string normalizedLabel)
    {
        return string.Equals(normalizedLabel, "OK", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedLabel, "Yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedLabel, "Restart", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedLabel, "Continue", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedLabel, "Load I/O Devices", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNegativeButton(string normalizedLabel)
    {
        return string.Equals(normalizedLabel, "No", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedLabel, "Cancel", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryInvokeButton(TwinCatModalDialogButton button)
    {
        try
        {
            AutomationElement? element = button.Element ?? TryGetElementFromHandle(button.Handle);
            if (element is null || !element.TryGetCurrentPattern(InvokePattern.Pattern, out object pattern))
            {
                return false;
            }

            ((InvokePattern)pattern).Invoke();
            return true;
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static bool WaitForDialogToClose(TwinCatModalDialog dialog)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(DialogDismissTimeoutMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            if (IsDialogClosed(dialog))
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return IsDialogClosed(dialog);
    }

    private static bool IsDialogClosed(TwinCatModalDialog dialog)
    {
        try
        {
            AutomationElement? element = dialog.Element ?? TryGetElementFromHandle(dialog.Handle);
            return element is null || element.Current.IsOffscreen;
        }
        catch (ElementNotAvailableException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        catch (COMException)
        {
            return true;
        }
    }

    private static bool LooksLikeMainWindow(string title)
    {
        return title.EndsWith(" - TcXaeShell", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecognizedDialog(TwinCatModalDialog dialog)
    {
        return IsRecognizedContent(dialog.Title, dialog.Text);
    }

    private static bool IsRecognizedContent(string title, string text)
    {
        string content = $"{title}\n{text}";
        return content.Contains("Restart TwinCAT System in Config Mode", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Load I/O Devices", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Activate Free Run", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetProcessId(AutomationElement element)
    {
        try
        {
            return element.Current.ProcessId;
        }
        catch (ElementNotAvailableException)
        {
            return 0;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
        catch (COMException)
        {
            return 0;
        }
    }

    private static ControlType? GetControlType(AutomationElement element)
    {
        try
        {
            return element.Current.ControlType;
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string GetName(AutomationElement element)
    {
        try
        {
            return element.Current.Name ?? string.Empty;
        }
        catch (ElementNotAvailableException)
        {
            return string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
        catch (COMException)
        {
            return string.Empty;
        }
    }

    private static IntPtr GetNativeWindowHandle(AutomationElement element)
    {
        try
        {
            return new IntPtr(element.Current.NativeWindowHandle);
        }
        catch (ElementNotAvailableException)
        {
            return IntPtr.Zero;
        }
        catch (InvalidOperationException)
        {
            return IntPtr.Zero;
        }
        catch (COMException)
        {
            return IntPtr.Zero;
        }
    }

    private static string GetElementKey(AutomationElement element)
    {
        IntPtr handle = GetNativeWindowHandle(element);
        if (handle != IntPtr.Zero)
        {
            return $"hwnd:{handle}";
        }

        try
        {
            return $"runtime:{string.Join(".", element.GetRuntimeId())}";
        }
        catch (ElementNotAvailableException)
        {
            return Guid.NewGuid().ToString("N");
        }
        catch (InvalidOperationException)
        {
            return Guid.NewGuid().ToString("N");
        }
        catch (COMException)
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
