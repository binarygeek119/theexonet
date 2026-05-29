using System;
using Rava.Core.Events;
using Rava.Mining;
using Rava.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Rava.UI
{
    public class LoginPanel : MonoBehaviour
    {
        private GameSession _session;
        private InputField _usernameInput;
        private InputField _emailInput;
        private InputField _passwordInput;
        private Text _statusText;
        private bool _isRegisterMode;

        public event Action OnLoginSuccess;

        public void Initialize(GameSession session, Transform parent)
        {
            _session = session;
            var panel = UIFactory.CreatePanel(parent, "LoginPanel", new Color(0.08f, 0.1f, 0.14f, 0.95f));

            var title = UIFactory.CreateText(panel.transform, "Title", "RAVA — Space Mining Corp", 28, TextAnchor.UpperCenter);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.1f, 0.75f);
            titleRect.anchorMax = new Vector2(0.9f, 0.9f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            _usernameInput = CreateLabeledInput(panel.transform, "Username", 0.62f);
            _emailInput = CreateLabeledInput(panel.transform, "Email (register only)", 0.52f);
            _emailInput.gameObject.SetActive(false);
            _passwordInput = CreateLabeledInput(panel.transform, "Password", 0.42f);
            _passwordInput.contentType = InputField.ContentType.Password;

            _statusText = UIFactory.CreateText(panel.transform, "Status", "", 14, TextAnchor.MiddleCenter);
            var statusRect = _statusText.rectTransform;
            statusRect.anchorMin = new Vector2(0.1f, 0.3f);
            statusRect.anchorMax = new Vector2(0.9f, 0.36f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            _statusText.color = new Color(1f, 0.6f, 0.5f);

            var loginBtn = UIFactory.CreateButton(panel.transform, "LoginBtn", "Login", new Color(0.2f, 0.45f, 0.7f));
            SetButtonRect(loginBtn, 0.28f, 0.38f);
            loginBtn.onClick.AddListener(() => _ = SubmitAsync(false));

            var registerBtn = UIFactory.CreateButton(panel.transform, "RegisterBtn", "Create Account", new Color(0.25f, 0.55f, 0.35f));
            SetButtonRect(registerBtn, 0.18f, 0.28f);
            registerBtn.onClick.AddListener(() => _ = SubmitAsync(true));

            var toggleBtn = UIFactory.CreateButton(panel.transform, "ToggleBtn", "Switch to Register", new Color(0.3f, 0.3f, 0.35f));
            SetButtonRect(toggleBtn, 0.08f, 0.18f);
            toggleBtn.onClick.AddListener(ToggleMode);
        }

        private void ToggleMode()
        {
            _isRegisterMode = !_isRegisterMode;
            _emailInput.gameObject.SetActive(_isRegisterMode);
            _statusText.text = _isRegisterMode ? "Register a new mining corp account." : "";
        }

        private async System.Threading.Tasks.Task SubmitAsync(bool register)
        {
            _statusText.text = "Connecting...";
            try
            {
                if (register || _isRegisterMode)
                {
                    await _session.RegisterAsync(_usernameInput.text, _emailInput.text, _passwordInput.text);
                }
                else
                {
                    await _session.LoginAsync(_usernameInput.text, _passwordInput.text);
                }

                _statusText.text = "";
                gameObject.SetActive(false);
                OnLoginSuccess?.Invoke();
            }
            catch (ApiException ex)
            {
                _statusText.text = ex.Message;
                GameEvents.RaiseError(ex.Message);
            }
            catch (Exception ex)
            {
                _statusText.text = ex.Message;
                GameEvents.RaiseError(ex.Message);
            }
        }

        private static InputField CreateLabeledInput(Transform parent, string label, float yMax)
        {
            var container = new GameObject(label, typeof(RectTransform));
            container.transform.SetParent(parent, false);
            var rect = container.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.25f, yMax - 0.08f);
            rect.anchorMax = new Vector2(0.75f, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            UIFactory.CreateText(container.transform, "Label", label, 14, TextAnchor.MiddleLeft);
            var labelText = container.transform.Find("Label").GetComponent<Text>();
            var labelRect = labelText.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0.55f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var input = UIFactory.CreateInputField(container.transform, label);
            var inputRect = input.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 0f);
            inputRect.anchorMax = new Vector2(1f, 0.5f);
            inputRect.offsetMin = Vector2.zero;
            inputRect.offsetMax = Vector2.zero;
            return input;
        }

        private static void SetButtonRect(Button button, float yMin, float yMax)
        {
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.3f, yMin);
            rect.anchorMax = new Vector2(0.7f, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
