using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public partial class ErrorWindow : Form
{
    public static DialogResult ShowErrorDialog(string friendlyMessage, Exception ex, bool allowContinue)
    {
        var lang = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
        var dialog = new ErrorWindow(lang)
        {
            ShowContinue = allowContinue,
            Message = friendlyMessage,
            Error = ex,
        };
        var dialogResult = dialog.ShowDialog();
        if (dialogResult == DialogResult.Abort)
        {
            Environment.Exit(1);
        }
        return dialogResult;
    }

    public ErrorWindow()
    {
        InitializeComponent();
    }

    public ErrorWindow(string lang) : this()
    {
        WinFormsUtil.TranslateInterface(this, lang);
    }

    /// <summary>
    /// Gets or sets whether or not the "Continue" button is visible.
    /// </summary>
    /// <remarks>For UI exceptions, continuing could be safe.
    /// For application exceptions, continuing is not possible, so the button should not be shown.</remarks>
    public bool ShowContinue
    {
        get => B_Continue.Visible;
        set => B_Continue.Visible = value;
    }

    /// <summary>
    /// Friendly, context-specific method shown to the user.
    /// </summary>
    /// <remarks>This property is intended to be a user-friendly context-specific message about what went wrong.
    /// For example: "An error occurred while attempting to automatically load the save file."</remarks>
    public string Message
    {
        get => L_Message.Text;
        set => L_Message.Text = value;
    }

    public Exception Error
    {
        get => _error;
        set
        {
            _error = value;
            UpdateExceptionDetailsMessage();
        }
    }

    private Exception _error;

    private void UpdateExceptionDetailsMessage()
    {
        var details = new StringBuilder();
        details.AppendLine("Exception Details:");
        details.AppendLine(Redact(Error.ToString()));
        details.AppendLine();

        details.AppendLine("Loaded Assemblies:");
        details.AppendLine("--------------------");
        try
        {
            foreach (var item in AppDomain.CurrentDomain.GetAssemblies())
            {
                details.AppendLine(item.FullName);
                details.AppendLine(Redact(item.Location));
                details.AppendLine();
            }
        }
        catch (Exception ex)
        {
            details.AppendLine("An error occurred while listing the Loaded Assemblies:");
            details.AppendLine(Redact(ex.ToString()));
        }
        details.AppendLine("--------------------");

        // Include message in case it contains important information, like a file path.
        details.AppendLine("User Message:");
        details.AppendLine(Redact(Message));

        T_ExceptionDetails.Text = details.ToString();
    }

    /// <summary>
    /// Removes local paths from anything shown or copied out of this dialog.
    /// <para>
    /// A report is meant to be pasted into an issue or a chat, and three things in it leak where the
    /// user keeps their files: the stack trace carries the source paths the build was compiled with,
    /// every loaded assembly reports its full location, and the message may hold a ROM path. The
    /// file name and line number are what make a report useful, and both survive this.
    /// </para>
    /// </summary>
    internal static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        text = SourcePath().Replace(text, m => " in " + Path.GetFileName(m.Groups[1].Value) + m.Groups[2].Value);

        // Longest first, so the most specific directory wins over its own parents.
        var roots = new List<(string Path, string Label)>();
        void Add(string p, string label)
        {
            if (!string.IsNullOrWhiteSpace(p) && p.Length > 3) roots.Add((p.TrimEnd('\\', '/'), label));
        }

        Add(AppDomain.CurrentDomain.BaseDirectory, "<app>");
        Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "<user>");
        Add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "<localdata>");
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "<appdata>");
        Add(Path.GetTempPath(), "<temp>");

        foreach (var (path, label) in roots.OrderByDescending(r => r.Path.Length))
            text = text.Replace(path, label, StringComparison.OrdinalIgnoreCase);

        // Whatever is left - drive-rooted or already labelled - keeps only its last two segments.
        // That is enough to identify which file is involved without describing where it lives.
        text = LeftoverPath().Replace(text, m =>
        {
            var parts = m.Value.TrimEnd('.', ',', ';', '\'').Split('\\', '/');
            if (parts.Length <= 2) return m.Value;
            string tail = m.Value.Length > 0 && ".,;'".Contains(m.Value[^1]) ? m.Value[^1].ToString() : "";
            return "..." + Path.DirectorySeparatorChar +
                   string.Join(Path.DirectorySeparatorChar, parts[^2..]) + tail;
        });

        return text;
    }

    [GeneratedRegex(@" in ([A-Za-z]:\\[^\r\n:]*?\.(?:cs|vb|fs))(:line )", RegexOptions.IgnoreCase)]
    private static partial Regex SourcePathInner();

    private static Regex SourcePath() => SourcePathInner();

    [GeneratedRegex(@"(?:[A-Za-z]:\\|<[a-z]+>\\)[^\r\n'""<>|?*]+", RegexOptions.IgnoreCase)]
    private static partial Regex LeftoverPathInner();

    private static Regex LeftoverPath() => LeftoverPathInner();

    private void B_CopyToClipboard_Click(object sender, EventArgs e)
    {
        Clipboard.SetText(T_ExceptionDetails.Text);
    }

    private void B_Continue_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    private void B_Abort_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Abort;
        Close();
    }
}
