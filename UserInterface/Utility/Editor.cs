﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ICSharpCode.TextEditor.Document;

namespace Utility
{
    /// <summary>
    /// This class provides an intellisense editor and has the option of syntax highlighting keywords.
    /// </summary>
    public partial class Editor : UserControl
    {
        private Form CompletionForm;
        private ListBox CompletionList;

        public class NeedContextItems : EventArgs
        {
            public string ObjectName;
            public List<string> Items;
        }
        public event EventHandler<NeedContextItems> ContextItemsNeeded;
        public event EventHandler TextHasChangedByUser;

        /// <summary>
        /// Constructor
        /// </summary>
        public Editor()
        {
            InitializeComponent();

            CompletionForm = new Form();
            CompletionForm.TopLevel = false;
            CompletionForm.FormBorderStyle = FormBorderStyle.None;
            CompletionList = new ListBox();
            CompletionList.Dock = DockStyle.Fill;
            CompletionForm.Controls.Add(CompletionList);
            CompletionList.KeyDown += new KeyEventHandler(OnContextListKeyDown);
            CompletionList.MouseDoubleClick += new MouseEventHandler(OnComtextListMouseDoubleClick);
            CompletionForm.StartPosition = FormStartPosition.Manual;

            TextBox.ActiveTextAreaControl.TextArea.KeyDown += OnKeyDown;
        }


        /// <summary>
        /// Text property to get and set the content of the editor.
        /// </summary>
        public new string Text
        {
            get
            {
                return TextBox.Text;
            }
            set
            {
                TextBox.TextChanged -= OnTextHasChanged;
                TextBox.Text = value;
                TextBox.TextChanged += OnTextHasChanged;
                TextBox.Document.HighlightingStrategy = HighlightingStrategyFactory.CreateHighlightingStrategy("C#");
            }
        }



        /// <summary>
        /// Lines property to get and set the lines in the editor.
        /// </summary>
        public string[] Lines
        {
            get
            {
                return TextBox.Text.Split(new string[1] { "\r\n" }, StringSplitOptions.None);
            }
            set
            {
                string St = "";
                foreach (string Value in value)
                    St += Value + "\r\n";
                Text = St;
            }
        }

        /// <summary>
        /// Preprocesses key strokes so that the ContextList can be displayed when needed.
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // If user clicks a '.' then display contextlist.
            if (e.KeyCode == Keys.OemPeriod && ContextItemsNeeded != null)
            {
                TextBox.ActiveTextAreaControl.TextArea.InsertChar('.');
                ShowCompletionWindow();
                e.Handled = false;
            }

            else
                e.Handled = false;
        }

        /// <summary>
        /// Retrieve the word before the specified character position.
        /// </summary>
        private string GetWordBeforePosition(int Pos)
        {
            if (Pos == 0)
                return "";
            else
            {
                int PosDelimiter = TextBox.Text.LastIndexOfAny(" \r\n".ToCharArray(), Pos - 1);
                return TextBox.Text.Substring(PosDelimiter + 1, Pos - PosDelimiter - 1).TrimEnd(".".ToCharArray());
            }
        }

        /// <summary>
        /// Show the context list.
        /// </summary>
        private void ShowCompletionWindow()
        {
            // Get a list of items to show and put into completion window.
            string TextBeforePeriod = GetWordBeforePosition(TextBox.ActiveTextAreaControl.TextArea.Caret.Offset);
            List<string> Items = new List<string>();
            ContextItemsNeeded(this, new NeedContextItems() { ObjectName = TextBeforePeriod, Items = Items });
            CompletionList.Items.Clear();
            CompletionList.Items.AddRange(Items.ToArray());

            // Turn readonly on so that the editing window doesn't process keystrokes.
            TextBox.Document.ReadOnly = true;

            // Work out where to put the completion window.
            Point p = TextBox.ActiveTextAreaControl.TextArea.Caret.ScreenPosition;
            Point EditorLocation = TextBox.PointToScreen(p);

            // Display completion window.
            CompletionForm.Parent = TextBox.ActiveTextAreaControl;
            CompletionForm.Left = p.X;
            CompletionForm.Top = p.Y + 20;  // Would be nice not to use a constant number of pixels.
            CompletionForm.Show();
            CompletionForm.BringToFront();
            CompletionForm.Controls[0].Focus();

            if (CompletionList.Items.Count > 0)
                CompletionList.SelectedIndex = 0;
        }

        /// <summary>
        /// Hide the completion window.
        /// </summary>
        private void HideCompletionWindow()
        {
            CompletionForm.Visible = false;
            TextBox.Document.ReadOnly = false;
        }

        void OnContextListKeyDown(object sender, KeyEventArgs e)
        {
            // If user clicks ENTER and the context list is visible then insert the currently
            // selected item from the list into the TextBox and close the list.
            if (e.KeyCode == Keys.Enter && CompletionList.Visible && CompletionList.SelectedIndex != -1)
            {
                InsertCompletionItemIntoTextBox();
                e.Handled = true;
            }

            // If the user presses ESC and the context list is visible then close the list.
            else if (e.KeyCode == Keys.Escape && CompletionList.Visible)
            {
                HideCompletionWindow();
                e.Handled = true;
            }
        }

        /// <summary>
        /// User has double clicked on a completion list item. 
        /// </summary>
        void OnComtextListMouseDoubleClick(object sender, MouseEventArgs e)
        {
            InsertCompletionItemIntoTextBox();
        }

        /// <summary>
        /// Insert the currently selected completion item into the text box.
        /// </summary>
        private void InsertCompletionItemIntoTextBox()
        {
            int Line = TextBox.ActiveTextAreaControl.TextArea.Caret.Line;
            int Column = TextBox.ActiveTextAreaControl.TextArea.Caret.Column;
            string TextToInsert = CompletionList.SelectedItem as string;
            TextBox.Text = TextBox.Text.Insert(TextBox.ActiveTextAreaControl.TextArea.Caret.Offset, TextToInsert);

            HideCompletionWindow();

            TextBox.ActiveTextAreaControl.TextArea.Caret.Line = Line;
            TextBox.ActiveTextAreaControl.TextArea.Caret.Column = Column + TextToInsert.Length;
        }


        /// <summary>
        /// User has changed text. Invoke our OnTextChanged event.
        /// </summary>
        private void OnTextHasChanged(object sender, EventArgs e)
        {
            if (TextHasChangedByUser != null)
                TextHasChangedByUser(sender, e);
        }

        #region Functions needed by the SyntaxHighlighter
        //public string GetLastWord()
        //{
        //    int pos = TextBox.SelectionStart;

        //    while (pos > 1)
        //    {
        //        string substr = Text.Substring(pos - 1, 1);

        //        if (Char.IsWhiteSpace(substr, 0))
        //        {
        //            return Text.Substring(pos, TextBox.SelectionStart - pos);
        //        }

        //        pos--;
        //    }

        //    return Text.Substring(0, TextBox.SelectionStart);
        //}
        //public string GetLastLine()
        //{
        //    int charIndex = TextBox.SelectionStart;
        //    int currentLineNumber = TextBox.GetLineFromCharIndex(charIndex);

        //    // the carriage return hasn't happened yet... 
        //    //      so the 'previous' line is the current one.
        //    string previousLineText;
        //    if (TextBox.Lines.Length <= currentLineNumber)
        //        previousLineText = TextBox.Lines[TextBox.Lines.Length - 1];
        //    else
        //        previousLineText = TextBox.Lines[currentLineNumber];

        //    return previousLineText;
        //}
        //public string GetCurrentLine()
        //{
        //    int charIndex = TextBox.SelectionStart;
        //    int currentLineNumber = TextBox.GetLineFromCharIndex(charIndex);

        //    if (currentLineNumber < TextBox.Lines.Length)
        //    {
        //        return TextBox.Lines[currentLineNumber];
        //    }
        //    else
        //    {
        //        return string.Empty;
        //    }
        //}
        //public int GetCurrentLineStartIndex()
        //{
        //    return TextBox.GetFirstCharIndexOfCurrentLine();
        //}
        //public int SelectionStart
        //{
        //    get
        //    {
        //        return TextBox.SelectionStart;
        //    }
        //    set
        //    {
        //        TextBox.SelectionStart = value;
        //    }
        //}
        //public int SelectionLength
        //{
        //    get
        //    {
        //        return TextBox.SelectionLength;
        //    }
        //    set
        //    {
        //        TextBox.SelectionLength = value;
        //    }
        //}
        //public Color SelectionColor
        //{
        //    get
        //    {
        //        return TextBox.SelectionColor;
        //    }
        //    set
        //    {
        //        TextBox.SelectionColor = value;
        //    }
        //}
        //public string SelectedText
        //{
        //    get
        //    {
        //        return TextBox.SelectedText;
        //    }
        //    set
        //    {
        //        TextBox.SelectedText = value;
        //    }
        //}
        #endregion
    }
}