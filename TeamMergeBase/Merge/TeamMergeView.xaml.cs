using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TeamMergeBase.Merge
{
    /// <summary>
    /// Interaction logic for TeamMergeView.xaml
    /// </summary>
    public partial class TeamMergeView
        : UserControl
    {
        public TeamMergeView()
        {
            //necessary to provoke dll from loading...
            var trig = new Microsoft.Xaml.Behaviors.EventTrigger(); trig.SourceName = "foo";

            InitializeComponent();
        }

        private void NumericTextInput(object sender, TextCompositionEventArgs e) => e.Handled = !IsTextAllowed(e.Text);

        private static readonly Regex _regex = new Regex(@"\D");
        private static bool IsTextAllowed(string text) => !_regex.IsMatch(text);
        private void TextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if (!IsTextAllowed(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

    }
}