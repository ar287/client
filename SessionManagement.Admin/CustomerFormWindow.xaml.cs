using System.Windows;
using System.Threading.Tasks;
using SessionManagement.Admin.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Admin
{
    public partial class CustomerFormWindow : Window
    {
        private readonly ApiService  _apiService;
        private readonly CustomerDto? _existingCustomer;
        private readonly bool         _isEditMode;

        public CustomerFormWindow(CustomerDto? customer)
        {
            InitializeComponent();
            _apiService       = new ApiService();
            _existingCustomer = customer;
            _isEditMode       = customer != null;

            SetupForm();
        }

        private void SetupForm()
        {
            if (_isEditMode && _existingCustomer != null)
            {
                // Edit mode
                this.Title           = "Edit Customer";
                FormTitleIcon.Text   = "✏️";
                FormTitle.Text       = "Edit Customer";
                SaveButtonText.Text  = "Save Changes";
                SaveButtonIcon.Text  = "💾  ";
                PasswordLabel.Text   = "NEW PASSWORD (leave blank to keep)";
                PasswordHint.Text    =
                    "Leave blank to keep existing password. " +
                    "Min 6 chars if changing.";

                // Pre-fill fields
                FullNameBox.Text  = _existingCustomer.FullName;
                UsernameBox.Text  = _existingCustomer.Username;
            }
            else
            {
                // Add mode
                this.Title = "Add New Customer";
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            // Hide previous messages
            ErrorBorder.Visibility   = Visibility.Collapsed;
            SuccessBorder.Visibility = Visibility.Collapsed;

            string fullName = FullNameBox.Text.Trim();
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password;

            // Validation
            if (string.IsNullOrWhiteSpace(fullName))
            {
                ShowError("Full name is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("Username is required.");
                return;
            }

            if (username.Contains(" "))
            {
                ShowError("Username cannot contain spaces.");
                return;
            }

            SaveButton.IsEnabled    = false;
            SaveButtonText.Text     = "Saving...";

            CustomerActionResponse? response;

            if (_isEditMode && _existingCustomer != null)
            {
                response = await new ApiService().UpdateCustomerAsync(
                    new UpdateCustomerRequest
                    {
                        UserId   = _existingCustomer.UserId,
                        FullName = fullName,
                        Username = username,
                        Password = password
                    });
            }
            else
            {
                if (string.IsNullOrWhiteSpace(password))
                {
                    ShowError("Password is required for new customers.");
                    SaveButton.IsEnabled = true;
                    SaveButtonText.Text  = "Save Customer";
                    return;
                }

                response = await new ApiService().CreateCustomerAsync(
                    new CreateCustomerRequest
                    {
                        FullName = fullName,
                        Username = username,
                        Password = password
                    });
            }

            SaveButton.IsEnabled = true;
            SaveButtonText.Text  = _isEditMode ? "Save Changes" : "Save Customer";

            if (response != null && response.Success)
            {
                ShowSuccess(response.Message);

                // Close after 1 second so user sees success message
                await Task.Delay(1000);
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                ShowError(response?.Message ?? "An error occurred.");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ShowError(string message)
        {
            ErrorMessage.Text        = message;
            ErrorBorder.Visibility   = Visibility.Visible;
            SuccessBorder.Visibility = Visibility.Collapsed;
        }

        private void ShowSuccess(string message)
        {
            SuccessMessage.Text      = message;
            SuccessBorder.Visibility = Visibility.Visible;
            ErrorBorder.Visibility   = Visibility.Collapsed;
        }
    }
}
