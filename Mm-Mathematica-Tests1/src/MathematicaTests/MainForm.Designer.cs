namespace MathematicaTests;

partial class MainForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        splitContainerMain = new SplitContainer();
        textBoxOutput = new TextBox();
        tabControlInteractions = new TabControl();
        tabPageBasics = new TabPage();
        buttonEvaluateSimple = new Button();
        ((System.ComponentModel.ISupportInitialize)splitContainerMain).BeginInit();
        splitContainerMain.Panel1.SuspendLayout();
        splitContainerMain.Panel2.SuspendLayout();
        splitContainerMain.SuspendLayout();
        tabControlInteractions.SuspendLayout();
        tabPageBasics.SuspendLayout();
        SuspendLayout();
        //
        // splitContainerMain
        //
        splitContainerMain.Dock = DockStyle.Fill;
        splitContainerMain.FixedPanel = FixedPanel.Panel2;
        splitContainerMain.Location = new Point(0, 0);
        splitContainerMain.Name = "splitContainerMain";
        //
        // splitContainerMain.Panel1
        //
        splitContainerMain.Panel1.Controls.Add(textBoxOutput);
        //
        // splitContainerMain.Panel2
        //
        splitContainerMain.Panel2.Controls.Add(tabControlInteractions);
        splitContainerMain.Size = new Size(984, 561);
        splitContainerMain.SplitterDistance = 680;
        splitContainerMain.TabIndex = 0;
        //
        // textBoxOutput
        //
        textBoxOutput.Dock = DockStyle.Fill;
        textBoxOutput.Font = new Font("Consolas", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        textBoxOutput.Location = new Point(0, 0);
        textBoxOutput.Multiline = true;
        textBoxOutput.Name = "textBoxOutput";
        textBoxOutput.ReadOnly = true;
        textBoxOutput.ScrollBars = ScrollBars.Both;
        textBoxOutput.Size = new Size(680, 561);
        textBoxOutput.TabIndex = 0;
        textBoxOutput.WordWrap = false;
        //
        // tabControlInteractions
        //
        tabControlInteractions.Controls.Add(tabPageBasics);
        tabControlInteractions.Dock = DockStyle.Fill;
        tabControlInteractions.Location = new Point(0, 0);
        tabControlInteractions.Name = "tabControlInteractions";
        tabControlInteractions.SelectedIndex = 0;
        tabControlInteractions.Size = new Size(300, 561);
        tabControlInteractions.TabIndex = 0;
        //
        // tabPageBasics
        //
        tabPageBasics.Controls.Add(buttonEvaluateSimple);
        tabPageBasics.Location = new Point(4, 24);
        tabPageBasics.Name = "tabPageBasics";
        tabPageBasics.Padding = new Padding(3);
        tabPageBasics.Size = new Size(292, 533);
        tabPageBasics.TabIndex = 0;
        tabPageBasics.Text = "Basics";
        tabPageBasics.UseVisualStyleBackColor = true;
        //
        // buttonEvaluateSimple
        //
        buttonEvaluateSimple.Location = new Point(16, 16);
        buttonEvaluateSimple.Name = "buttonEvaluateSimple";
        buttonEvaluateSimple.Size = new Size(260, 32);
        buttonEvaluateSimple.TabIndex = 0;
        buttonEvaluateSimple.Text = "Evaluate 2+2";
        buttonEvaluateSimple.UseVisualStyleBackColor = true;
        buttonEvaluateSimple.Click += buttonEvaluateSimple_Click;
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(984, 561);
        Controls.Add(splitContainerMain);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Mathematica .NET/Link Tests";
        FormClosed += MainForm_FormClosed;
        splitContainerMain.Panel1.ResumeLayout(false);
        splitContainerMain.Panel1.PerformLayout();
        splitContainerMain.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitContainerMain).EndInit();
        splitContainerMain.ResumeLayout(false);
        tabControlInteractions.ResumeLayout(false);
        tabPageBasics.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private SplitContainer splitContainerMain;
    private TextBox textBoxOutput;
    private TabControl tabControlInteractions;
    private TabPage tabPageBasics;
    private Button buttonEvaluateSimple;
}
