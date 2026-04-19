using System.ComponentModel;
using System.Data.SqlClient;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace SsmsRestoreDrop.UI
{
    public partial class ConnectDialog : Window, INotifyPropertyChanged
    {
        private string _errorMessage = string.Empty;

        public string ConnectionString { get; private set; } = string.Empty;

        public string ErrorMessage
        {
            get => _errorMessage;
            private set { _errorMessage = value; OnPropertyChanged(); }
        }

        public ConnectDialog() { InitializeComponent(); DataContext = this; UpdateAuthVisibility(); }

        private void Auth_Changed(object sender, SelectionChangedEventArgs e)
            => UpdateAuthVisibility();

        private void UpdateAuthVisibility()
        {
            // Auth_Changed fires during XAML load (from ComboBoxItem IsSelected="True")
            // before the other named elements have been hooked up.
            if (LoginLabel == null || LoginBox == null || PwdLabel == null || PwdBox == null)
                return;

            bool sql = AuthBox.SelectedIndex == 1;
            LoginLabel.Visibility = sql ? Visibility.Visible : Visibility.Collapsed;
            LoginBox.Visibility   = sql ? Visibility.Visible : Visibility.Collapsed;
            PwdLabel.Visibility   = sql ? Visibility.Visible : Visibility.Collapsed;
            PwdBox.Visibility     = sql ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            ErrorMessage = string.Empty;
            var server = ServerBox.Text.Trim();
            if (string.IsNullOrEmpty(server)) { ErrorMessage = "Server name is required."; return; }

            var csb = new SqlConnectionStringBuilder
            {
                DataSource      = server,
                ApplicationName = "SsmsQuickRestore",
                ConnectTimeout  = 15
            };

            if (AuthBox.SelectedIndex == 0)
            {
                csb.IntegratedSecurity = true;
            }
            else
            {
                csb.IntegratedSecurity = false;
                csb.UserID             = LoginBox.Text.Trim();
                csb.Password           = PwdBox.Password;
            }

            // Quick connectivity test
            try
            {
                using var conn = new SqlConnection(csb.ConnectionString);
                conn.Open();
            }
            catch (SqlException ex)
            {
                ErrorMessage = $"Connection failed: {ex.Message}";
                return;
            }

            ConnectionString = csb.ConnectionString;
            DialogResult     = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
