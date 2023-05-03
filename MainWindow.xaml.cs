using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Translator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SourceTextBox.Text.Length == 0) // оказывается, у string нет метода Empty
                {
                    MessageBox.Show("Введите исходный код.");
                    return;
                }

                Lexer l = new();

                List<(string, int)> tokenList = l.Run(SourceTextBox.Text);

                RPN rpn = new(tokenList, l.KeyWordTable, l.OpTable,
                    l.SepTable, l.IdentifierTable, l.ConstNumTable,
                    l.ConstCharTable);

                TokensTextBox.Text = rpn.GetRPN();

                KeywordsTextBox.Text = Lexer.TableToString(l.KeyWordTable);
                OpsTextBox.Text = Lexer.TableToString(l.OpTable);
                SepsTextBox.Text = Lexer.TableToString(l.SepTable);
                IDsTextBox.Text = Lexer.TableToString(l.IdentifierTable);
                CNumsTextBox.Text = Lexer.TableToString(l.ConstNumTable);
                CCharsTextBox.Text = Lexer.TableToString(l.ConstCharTable);
            }
            catch(Exception exc)
            {
                MessageBox.Show(exc.Message);
            }

        }
    }
}
