using System;
using System.Collections.Generic;
using System.Globalization;
using Theexonet.Mining;
using Theexonet.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Theexonet.UI
{
    public class LoginPanel : MonoBehaviour
    {
        private enum AuthMode
        {
            Login,
            Register,
            ForgotPassword,
            ResetPassword
        }

        private enum StatusKind
        {
            Info,
            Success,
            Error
        }

        private const float FieldHeight = 0.08f;
        private const float BirthdayFieldHeight = 0.10f;
        private const float BirthdayComboPickerWidth = 0.2f;
        private const int BirthdayControlFontSize = 12;
        private const float BirthdayListItemHeight = 22f;
        private const float BirthdayListHeight = 110f;
        private const float ButtonHeight = 0.05f;
        private const float StatusHeight = 0.05f;

        private GameSession _session;
        private InputField _usernameInput;
        private InputField _emailInput;
        private InputField _birthdayMonthInput;
        private Dropdown _birthdayMonthDropdown;
        private InputField _birthdayDayInput;
        private Dropdown _birthdayDayDropdown;
        private InputField _birthdayYearInput;
        private Dropdown _birthdayYearDropdown;
        private InputField _passwordInput;
        private InputField _resetTokenInput;
        private InputField _confirmPasswordInput;
        private RectTransform _usernameField;
        private RectTransform _emailField;
        private RectTransform _birthdayField;
        private RectTransform _passwordField;
        private RectTransform _resetTokenField;
        private RectTransform _confirmPasswordField;
        private RectTransform _statusRect;
        private RectTransform _loginButtonRect;
        private RectTransform _registerButtonRect;
        private RectTransform _toggleButtonRect;
        private RectTransform _forgotButtonRect;
        private RectTransform _resetTokenButtonRect;
        private RectTransform _sendResetButtonRect;
        private RectTransform _resetPasswordButtonRect;
        private RectTransform _backButtonRect;
        private Text _statusText;
        private Button _loginButton;
        private Button _registerButton;
        private Button _toggleButton;
        private AuthMode _authMode = AuthMode.Login;

        public event Action OnLoginSuccess;

        public void Initialize(GameSession session)
        {
            _session = session;
            var panel = UIFactory.CreatePanel(transform, "LoginPanel", new Color(0.08f, 0.1f, 0.14f, 0.95f));

            var title = UIFactory.CreateText(panel.transform, "Title", "theexonet", 28, TextAnchor.UpperCenter);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.1f, 0.86f);
            titleRect.anchorMax = new Vector2(0.9f, 0.94f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            var subtitle = UIFactory.CreateText(
                panel.transform,
                "Subtitle",
                "Point-and-click asteroid mining",
                14,
                TextAnchor.UpperCenter);
            subtitle.color = new Color(0.75f, 0.8f, 0.9f);
            var subtitleRect = subtitle.rectTransform;
            subtitleRect.anchorMin = new Vector2(0.08f, 0.80f);
            subtitleRect.anchorMax = new Vector2(0.92f, 0.86f);
            subtitleRect.offsetMin = Vector2.zero;
            subtitleRect.offsetMax = Vector2.zero;

            (_usernameField, _usernameInput) = CreateLabeledInput(panel.transform, "Username", "Username");
            (_emailField, _emailInput) = CreateLabeledInput(panel.transform, "Email", "Email");
            (_birthdayField, _birthdayMonthInput, _birthdayMonthDropdown, _birthdayDayInput, _birthdayDayDropdown, _birthdayYearInput, _birthdayYearDropdown) =
                CreateBirthdayInputs(panel.transform);
            (_passwordField, _passwordInput) = CreateLabeledInput(panel.transform, "Password", "Password");
            _passwordInput.contentType = InputField.ContentType.Password;
            (_resetTokenField, _resetTokenInput) = CreateLabeledInput(panel.transform, "ResetToken", "Reset token from email");
            (_confirmPasswordField, _confirmPasswordInput) = CreateLabeledInput(panel.transform, "ConfirmPassword", "Confirm new password");
            _confirmPasswordInput.contentType = InputField.ContentType.Password;

            _statusText = UIFactory.CreateText(panel.transform, "Status", "", 14, TextAnchor.MiddleCenter);
            _statusRect = _statusText.rectTransform;
            SetStatus(string.Empty, StatusKind.Info);

            _loginButton = UIFactory.CreateButton(panel.transform, "LoginBtn", "Login", new Color(0.2f, 0.45f, 0.7f));
            _loginButtonRect = _loginButton.GetComponent<RectTransform>();
            _loginButton.onClick.AddListener(() => _ = SubmitAsync(false));

            _registerButton = UIFactory.CreateButton(panel.transform, "RegisterBtn", "Create Account", new Color(0.25f, 0.55f, 0.35f));
            _registerButtonRect = _registerButton.GetComponent<RectTransform>();
            _registerButton.onClick.AddListener(() => _ = SubmitAsync(true));

            _toggleButton = UIFactory.CreateButton(panel.transform, "ToggleBtn", "Switch to Register", new Color(0.3f, 0.3f, 0.35f));
            _toggleButtonRect = _toggleButton.GetComponent<RectTransform>();
            _toggleButton.onClick.AddListener(ToggleRegisterMode);

            var forgotBtn = UIFactory.CreateButton(panel.transform, "ForgotBtn", "Forgot password?", new Color(0.3f, 0.3f, 0.35f));
            _forgotButtonRect = forgotBtn.GetComponent<RectTransform>();
            forgotBtn.onClick.AddListener(() => SetAuthMode(AuthMode.ForgotPassword));

            var resetTokenBtn = UIFactory.CreateButton(panel.transform, "ResetTokenBtn", "Have reset token?", new Color(0.3f, 0.3f, 0.35f));
            _resetTokenButtonRect = resetTokenBtn.GetComponent<RectTransform>();
            resetTokenBtn.onClick.AddListener(() => SetAuthMode(AuthMode.ResetPassword));

            var sendResetBtn = UIFactory.CreateButton(panel.transform, "SendResetBtn", "Send reset link", new Color(0.2f, 0.45f, 0.7f));
            _sendResetButtonRect = sendResetBtn.GetComponent<RectTransform>();
            sendResetBtn.onClick.AddListener(() => _ = SendResetAsync());

            var resetPasswordBtn = UIFactory.CreateButton(panel.transform, "ResetPasswordBtn", "Update password", new Color(0.2f, 0.45f, 0.7f));
            _resetPasswordButtonRect = resetPasswordBtn.GetComponent<RectTransform>();
            resetPasswordBtn.onClick.AddListener(() => _ = ResetPasswordAsync());

            var backBtn = UIFactory.CreateButton(panel.transform, "BackBtn", "Back to login", new Color(0.3f, 0.3f, 0.35f));
            _backButtonRect = backBtn.GetComponent<RectTransform>();
            backBtn.onClick.AddListener(() => SetAuthMode(AuthMode.Login));

            ShrinkButtonLabel(_loginButton, 16);
            ShrinkButtonLabel(_registerButton, 16);
            ShrinkButtonLabel(_toggleButton, 14);
            ShrinkButtonLabel(forgotBtn, 13);
            ShrinkButtonLabel(resetTokenBtn, 13);
            ShrinkButtonLabel(sendResetBtn, 15);
            ShrinkButtonLabel(resetPasswordBtn, 15);
            ShrinkButtonLabel(backBtn, 14);

            WireSubmitOnEnter(_usernameInput);
            WireSubmitOnEnter(_emailInput);
            WireSubmitOnEnter(_passwordInput);
            WireSubmitOnEnter(_resetTokenInput);
            WireSubmitOnEnter(_confirmPasswordInput);

            SetAuthMode(AuthMode.Login);
        }

        private void WireSubmitOnEnter(InputField input)
        {
            input.onSubmit.AddListener(_ => SubmitForCurrentMode());
        }

        private void SubmitForCurrentMode()
        {
            switch (_authMode)
            {
                case AuthMode.Register:
                    _ = SubmitAsync(true);
                    break;
                case AuthMode.ForgotPassword:
                    _ = SendResetAsync();
                    break;
                case AuthMode.ResetPassword:
                    _ = ResetPasswordAsync();
                    break;
                default:
                    _ = SubmitAsync(false);
                    break;
            }
        }

        private static void ShrinkButtonLabel(Button button, int fontSize)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = fontSize;
            }
        }

        private void ToggleRegisterMode()
        {
            SetAuthMode(_authMode == AuthMode.Register ? AuthMode.Login : AuthMode.Register);
        }

        private void SetAuthMode(AuthMode mode)
        {
            _authMode = mode;
            _usernameField.gameObject.SetActive(mode is AuthMode.Login or AuthMode.Register);
            _emailField.gameObject.SetActive(mode is AuthMode.Register or AuthMode.ForgotPassword);
            _birthdayField.gameObject.SetActive(mode == AuthMode.Register);
            _passwordField.gameObject.SetActive(mode is AuthMode.Login or AuthMode.Register or AuthMode.ResetPassword);
            _resetTokenField.gameObject.SetActive(mode == AuthMode.ResetPassword);
            _confirmPasswordField.gameObject.SetActive(mode == AuthMode.ResetPassword);

            _loginButton.gameObject.SetActive(mode == AuthMode.Login);
            _registerButton.gameObject.SetActive(mode == AuthMode.Register);
            _toggleButton.gameObject.SetActive(mode is AuthMode.Login or AuthMode.Register);
            _forgotButtonRect.gameObject.SetActive(mode == AuthMode.Login);
            _resetTokenButtonRect.gameObject.SetActive(mode == AuthMode.ForgotPassword);
            _sendResetButtonRect.gameObject.SetActive(mode == AuthMode.ForgotPassword);
            _resetPasswordButtonRect.gameObject.SetActive(mode == AuthMode.ResetPassword);
            _backButtonRect.gameObject.SetActive(mode is AuthMode.ForgotPassword or AuthMode.ResetPassword);

            _toggleButton.GetComponentInChildren<Text>().text =
                mode == AuthMode.Register ? "Switch to Login" : "Switch to Register";

            var hint = mode switch
            {
                AuthMode.Register => "Email and birthday are required to create an account.",
                AuthMode.ForgotPassword => "Enter the email on your account.",
                AuthMode.ResetPassword => "Paste the reset token from your email.",
                _ => string.Empty
            };
            SetStatus(hint, StatusKind.Info);

            ApplyLayout();
        }

        private void SetStatus(string message, StatusKind kind)
        {
            _statusText.text = message ?? string.Empty;
            _statusText.color = kind switch
            {
                StatusKind.Success => new Color(0.55f, 0.92f, 0.62f),
                StatusKind.Error => new Color(1f, 0.55f, 0.5f),
                _ => new Color(1f, 0.78f, 0.45f)
            };
        }

        private void ApplyLayout()
        {
            SetButtonRect(_loginButtonRect, 0.30f);
            SetButtonRect(_registerButtonRect, 0.22f);
            SetButtonRect(_toggleButtonRect, 0.14f);
            SetButtonRect(_forgotButtonRect, 0.08f);
            SetButtonRect(_sendResetButtonRect, 0.36f);
            SetButtonRect(_resetTokenButtonRect, 0.28f);
            SetButtonRect(_resetPasswordButtonRect, 0.22f);
            SetButtonRect(_backButtonRect, 0.14f);

            switch (_authMode)
            {
                case AuthMode.Register:
                    SetFieldRect(_usernameField, 0.74f);
                    SetFieldRect(_emailField, 0.64f);
                    SetBirthdayFieldRect(0.54f);
                    SetFieldRect(_passwordField, 0.38f);
                    SetStatusRect(0.26f);
                    break;
                case AuthMode.ForgotPassword:
                    SetFieldRect(_emailField, 0.66f);
                    SetStatusRect(0.50f);
                    break;
                case AuthMode.ResetPassword:
                    SetFieldRect(_resetTokenField, 0.72f);
                    SetFieldRect(_passwordField, 0.60f);
                    SetFieldRect(_confirmPasswordField, 0.48f);
                    SetStatusRect(0.36f);
                    break;
                default:
                    SetFieldRect(_usernameField, 0.68f);
                    SetFieldRect(_passwordField, 0.54f);
                    SetStatusRect(0.40f);
                    break;
            }
        }

        private async System.Threading.Tasks.Task SubmitAsync(bool register)
        {
            var isRegister = register || _authMode == AuthMode.Register;

            if (isRegister)
            {
                if (string.IsNullOrWhiteSpace(_usernameInput.text) || string.IsNullOrWhiteSpace(_passwordInput.text))
                {
                    SetStatus("Username and password are required.", StatusKind.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(_emailInput.text))
                {
                    SetStatus("Email is required to sign up.", StatusKind.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(GetBirthdayValue()))
                {
                    SetStatus("Birthday is required to sign up.", StatusKind.Error);
                    return;
                }

                if (_passwordInput.text.Length < 8)
                {
                    SetStatus("Password must be at least 8 characters.", StatusKind.Error);
                    return;
                }
            }

            SetStatus(isRegister ? "Creating account..." : "Connecting...", StatusKind.Info);
            try
            {
                if (isRegister)
                {
                    await _session.RegisterAccountAsync(
                        _usernameInput.text,
                        _emailInput.text,
                        _passwordInput.text,
                        GetBirthdayValue());
                    _passwordInput.text = string.Empty;
                    _emailInput.text = string.Empty;
                    ResetBirthdayInputs();
                    SetAuthMode(AuthMode.Login);
                    SetStatus("Account created successfully. Log in with your username and password.", StatusKind.Success);
                    return;
                }

                await _session.LoginAsync(_usernameInput.text, _passwordInput.text);

                SetStatus(string.Empty, StatusKind.Info);
                gameObject.SetActive(false);
                OnLoginSuccess?.Invoke();
            }
            catch (ApiException ex)
            {
                SetStatus(ex.Message, StatusKind.Error);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, StatusKind.Error);
            }
        }

        private async System.Threading.Tasks.Task SendResetAsync()
        {
            if (string.IsNullOrWhiteSpace(_emailInput.text))
            {
                SetStatus("Email is required.", StatusKind.Error);
                return;
            }

            SetStatus("Sending reset link...", StatusKind.Info);
            try
            {
                var response = await _session.ForgotPasswordAsync(_emailInput.text);
                SetStatus(response.message, StatusKind.Success);
            }
            catch (ApiException ex)
            {
                SetStatus(ex.Message, StatusKind.Error);
            }
        }

        private async System.Threading.Tasks.Task ResetPasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(_resetTokenInput.text) || string.IsNullOrWhiteSpace(_passwordInput.text))
            {
                SetStatus("Reset token and new password are required.", StatusKind.Error);
                return;
            }

            if (_passwordInput.text.Length < 8)
            {
                SetStatus("Password must be at least 8 characters.", StatusKind.Error);
                return;
            }

            if (_passwordInput.text != _confirmPasswordInput.text)
            {
                SetStatus("Passwords do not match.", StatusKind.Error);
                return;
            }

            SetStatus("Updating password...", StatusKind.Info);
            try
            {
                var response = await _session.ResetPasswordAsync(_resetTokenInput.text, _passwordInput.text);
                SetAuthMode(AuthMode.Login);
                SetStatus(response.message, StatusKind.Success);
            }
            catch (ApiException ex)
            {
                SetStatus(ex.Message, StatusKind.Error);
            }
        }

        private static (
            RectTransform field,
            InputField monthInput,
            Dropdown monthDropdown,
            InputField dayInput,
            Dropdown dayDropdown,
            InputField yearInput,
            Dropdown yearDropdown) CreateBirthdayInputs(Transform parent)
        {
            var container = new GameObject("Birthday", typeof(RectTransform));
            container.transform.SetParent(parent, false);

            UIFactory.CreateText(container.transform, "Label", "Birthday", 12, TextAnchor.MiddleLeft);
            var labelRect = container.transform.Find("Label").GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.88f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var month = CreateBirthdayCombo(
                container.transform,
                "BirthdayMonth",
                "Month",
                0f,
                0.40f,
                128f,
                PopulateBirthdayMonthOptions);
            var day = CreateBirthdayCombo(
                container.transform,
                "BirthdayDay",
                "Day",
                0.42f,
                0.62f,
                60f,
                dropdown => PopulateBirthdayDayOptions(dropdown));
            var year = CreateBirthdayCombo(
                container.transform,
                "BirthdayYear",
                "Year",
                0.64f,
                1f,
                80f,
                PopulateBirthdayYearOptions);

            var monthInput = month.input;
            var monthDropdown = month.dropdown;
            var dayInput = day.input;
            var dayDropdown = day.dropdown;
            var yearInput = year.input;
            var yearDropdown = year.dropdown;

            void RefreshDays() => RefreshBirthdayDayOptions(dayInput, dayDropdown, monthInput, yearInput);

            monthInput.onEndEdit.AddListener(_ => RefreshDays());
            yearInput.onEndEdit.AddListener(_ => RefreshDays());
            monthDropdown.onValueChanged.AddListener(_ => RefreshDays());
            yearDropdown.onValueChanged.AddListener(_ => RefreshDays());

            return (
                container.GetComponent<RectTransform>(),
                monthInput,
                monthDropdown,
                dayInput,
                dayDropdown,
                yearInput,
                yearDropdown);
        }

        private static (InputField input, Dropdown dropdown) CreateBirthdayCombo(
            Transform parent,
            string prefix,
            string placeholder,
            float xMin,
            float xMax,
            float listExtraWidth,
            Action<Dropdown> populateDropdown)
        {
            var combo = UIFactory.CreateComboField(
                parent,
                prefix,
                placeholder,
                listExtraWidth,
                BirthdayControlFontSize,
                BirthdayListItemHeight,
                BirthdayListHeight,
                BirthdayComboPickerWidth);
            SetBirthdayControlRect(combo.root, xMin, xMax, 0.12f, 0.86f);

            var input = combo.input;
            input.contentType = InputField.ContentType.Standard;
            var dropdown = combo.dropdown;
            populateDropdown(dropdown);

            dropdown.onValueChanged.AddListener(index =>
            {
                if (index <= 0)
                {
                    return;
                }

                input.text = dropdown.options[index].text;
                dropdown.value = 0;
                dropdown.RefreshShownValue();
            });

            return (input, dropdown);
        }

        private static void SetBirthdayControlRect(
            RectTransform rect,
            float xMin,
            float xMax,
            float yMin,
            float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void PopulateBirthdayMonthOptions(Dropdown dropdown)
        {
            var options = new List<string> { "..." };
            for (var month = 1; month <= 12; month++)
            {
                options.Add(CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month));
            }

            UIFactory.SetDropdownOptions(dropdown, options);
        }

        private static void PopulateBirthdayDayOptions(Dropdown dropdown, int maxDays = 31)
        {
            var options = new List<string> { "..." };
            for (var day = 1; day <= maxDays; day++)
            {
                options.Add(day.ToString(CultureInfo.InvariantCulture));
            }

            UIFactory.SetDropdownOptions(dropdown, options);
        }

        private static void PopulateBirthdayYearOptions(Dropdown dropdown)
        {
            var options = new List<string> { "..." };
            var currentYear = DateTime.UtcNow.Year;
            for (var year = currentYear - 13; year >= currentYear - 120; year--)
            {
                options.Add(year.ToString(CultureInfo.InvariantCulture));
            }

            UIFactory.SetDropdownOptions(dropdown, options);
        }

        private static void RefreshBirthdayDayOptions(
            InputField dayInput,
            Dropdown dayDropdown,
            InputField monthInput,
            InputField yearInput)
        {
            var previousDayText = dayInput.text;
            var maxDays = GetMaxBirthdayDays(monthInput, yearInput);
            PopulateBirthdayDayOptions(dayDropdown, maxDays);

            if (TryParseBirthdayDay(previousDayText, out var day) && day <= maxDays)
            {
                dayInput.text = day.ToString(CultureInfo.InvariantCulture);
            }
            else if (!string.IsNullOrWhiteSpace(previousDayText))
            {
                dayInput.text = string.Empty;
            }
        }

        private static int GetMaxBirthdayDays(InputField monthInput, InputField yearInput)
        {
            if (!TryParseBirthdayMonth(monthInput.text, out var month))
            {
                return 31;
            }

            if (!TryParseBirthdayYear(yearInput.text, out var year))
            {
                return DateTime.DaysInMonth(2000, month);
            }

            return DateTime.DaysInMonth(year, month);
        }

        private static bool TryParseBirthdayMonth(string text, out int month)
        {
            month = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();
            if (int.TryParse(trimmed, out month) && month is >= 1 and <= 12)
            {
                return true;
            }

            for (var candidate = 1; candidate <= 12; candidate++)
            {
                var culture = CultureInfo.InvariantCulture.DateTimeFormat;
                var fullName = culture.GetMonthName(candidate);
                var shortName = culture.GetAbbreviatedMonthName(candidate);
                if (fullName.Equals(trimmed, StringComparison.OrdinalIgnoreCase)
                    || shortName.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    month = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseBirthdayDay(string text, out int day)
        {
            day = 0;
            return !string.IsNullOrWhiteSpace(text)
                && int.TryParse(text.Trim(), out day)
                && day is >= 1 and <= 31;
        }

        private static bool TryParseBirthdayYear(string text, out int year)
        {
            year = 0;
            return !string.IsNullOrWhiteSpace(text)
                && int.TryParse(text.Trim(), out year)
                && year is >= 1900 and <= 9999;
        }

        private string GetBirthdayValue()
        {
            if (!TryParseBirthdayMonth(_birthdayMonthInput.text, out var month)
                || !TryParseBirthdayDay(_birthdayDayInput.text, out var day)
                || !TryParseBirthdayYear(_birthdayYearInput.text, out var year))
            {
                return string.Empty;
            }

            if (day > DateTime.DaysInMonth(year, month))
            {
                return string.Empty;
            }

            return $"{year:0000}-{month:00}-{day:00}";
        }

        private void ResetBirthdayInputs()
        {
            _birthdayMonthInput.text = string.Empty;
            _birthdayMonthDropdown.value = 0;
            _birthdayMonthDropdown.RefreshShownValue();
            _birthdayDayInput.text = string.Empty;
            PopulateBirthdayDayOptions(_birthdayDayDropdown);
            _birthdayDayDropdown.value = 0;
            _birthdayDayDropdown.RefreshShownValue();
            _birthdayYearInput.text = string.Empty;
            _birthdayYearDropdown.value = 0;
            _birthdayYearDropdown.RefreshShownValue();
        }

        private static (RectTransform container, InputField input) CreateLabeledInput(Transform parent, string label, string placeholder)
        {
            var container = new GameObject(label, typeof(RectTransform));
            container.transform.SetParent(parent, false);

            UIFactory.CreateText(container.transform, "Label", label, 14, TextAnchor.MiddleLeft);
            var labelText = container.transform.Find("Label").GetComponent<Text>();
            var labelRect = labelText.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0.55f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var input = UIFactory.CreateInputField(container.transform, placeholder);
            var inputRect = input.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 0f);
            inputRect.anchorMax = new Vector2(1f, 0.5f);
            inputRect.offsetMin = Vector2.zero;
            inputRect.offsetMax = Vector2.zero;

            return (container.GetComponent<RectTransform>(), input);
        }

        private static void SetFieldRect(RectTransform rect, float yMax, float height = FieldHeight)
        {
            rect.anchorMin = new Vector2(0.25f, yMax - height);
            rect.anchorMax = new Vector2(0.75f, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetBirthdayFieldRect(float yMax)
        {
            _birthdayField.anchorMin = new Vector2(0.12f, yMax - BirthdayFieldHeight);
            _birthdayField.anchorMax = new Vector2(0.88f, yMax);
            _birthdayField.offsetMin = Vector2.zero;
            _birthdayField.offsetMax = Vector2.zero;
        }

        private void SetStatusRect(float yMax)
        {
            _statusRect.anchorMin = new Vector2(0.1f, yMax - StatusHeight);
            _statusRect.anchorMax = new Vector2(0.9f, yMax);
            _statusRect.offsetMin = Vector2.zero;
            _statusRect.offsetMax = Vector2.zero;
        }

        private static void SetButtonRect(RectTransform rect, float yMax)
        {
            rect.anchorMin = new Vector2(0.28f, yMax - ButtonHeight);
            rect.anchorMax = new Vector2(0.72f, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
