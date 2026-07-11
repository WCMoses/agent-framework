namespace MathematicaTests;

public partial class MainForm : Form
{
    private readonly KernelSession _kernel = new();

    public MainForm()
    {
        InitializeComponent();
    }

    private void buttonEvaluateSimple_Click(object sender, EventArgs e)
    {
        EvaluateAndShow("2+2");
    }

    /// <summary>
    /// Sends an expression to the kernel and appends both the input and the
    /// result (or the error) to the output box. Future tabs can reuse this.
    /// </summary>
    private void EvaluateAndShow(string expression)
    {
        bool firstCall = !_kernel.IsStarted;
        if (firstCall)
        {
            AppendOutput("Launching Mathematica kernel (first call may take a few seconds)...");
        }

        UseWaitCursor = true;
        try
        {
            string result = _kernel.Evaluate(expression);
            AppendOutput($"In:  {expression}");
            AppendOutput($"Out: {result}");
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR evaluating \"{expression}\": {ex.Message}");
        }
        finally
        {
            UseWaitCursor = false;
        }

        AppendOutput(string.Empty);
    }

    private void AppendOutput(string line)
    {
        textBoxOutput.AppendText(line + Environment.NewLine);
    }

    private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
    {
        _kernel.Dispose();
    }
}
