using System.IO;
using Rava.Core.Dtos;
using Rava.Mining;
using Rava.Networking;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rava.UI
{
    public class ProfilePanel : MonoBehaviour
    {
        private GameSession _session;
        private GameObject _overlayRoot;
        private GameObject _panelRoot;
        private Text _headerText;
        private Text _statsText;
        private Text _profileNumberText;
        private Text _memberSinceText;
        private Text _statusText;
        private Text _avatarInitials;
        private RawImage _avatarImage;
        private InputField _moodInput;
        private InputField _aboutInput;
        private InputField _musicInput;
        private InputField _interestsInput;
        private bool _visible;

        public bool IsVisible => _visible;

        public void Initialize(GameSession session, Transform parent)
        {
            _session = session;
            _overlayRoot = UIFactory.CreatePanel(parent, "ProfileOverlay", new Color(0f, 0f, 0f, 0.72f));
            var overlayRect = _overlayRoot.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            _overlayRoot.SetActive(false);

            _panelRoot = UIFactory.CreatePanel(_overlayRoot.transform, "ProfilePanel", new Color(0.08f, 0.1f, 0.15f, 0.96f));
            var rect = _panelRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, 0.08f);
            rect.anchorMax = new Vector2(0.92f, 0.92f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _headerText = UIFactory.CreateText(_panelRoot.transform, "Title", "My Profile", 22, TextAnchor.MiddleCenter);
            SetRect(_headerText.rectTransform, 0.28f, 0.935f, 0.72f, 0.985f);

            var saveBtn = UIFactory.CreateButton(_panelRoot.transform, "SaveProfile", "Save Profile", new Color(0.2f, 0.45f, 0.3f));
            SetRect(saveBtn.GetComponent<RectTransform>(), 0.03f, 0.935f, 0.24f, 0.985f);
            saveBtn.onClick.AddListener(() => _ = SaveProfileAsync());

            var closeBtn = UIFactory.CreateButton(_panelRoot.transform, "CloseProfile", "Close", new Color(0.25f, 0.3f, 0.4f));
            SetRect(closeBtn.GetComponent<RectTransform>(), 0.76f, 0.935f, 0.97f, 0.985f);
            closeBtn.onClick.AddListener(() => SetVisible(false));

            var avatarFrame = UIFactory.CreatePanel(
                _panelRoot.transform,
                "AvatarFrame",
                new Color(0.15f, 0.2f, 0.35f, 1f));
            var avatarRect = avatarFrame.GetComponent<RectTransform>();
            SetSquareRect(avatarRect, 0.04f, 0.915f, 112f);
            avatarFrame.AddComponent<Mask>().showMaskGraphic = true;

            var avatarImageGo = new GameObject(
                "AvatarImage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            avatarImageGo.transform.SetParent(avatarFrame.transform, false);
            Stretch(avatarImageGo.GetComponent<RectTransform>());
            _avatarImage = avatarImageGo.GetComponent<RawImage>();
            _avatarImage.color = new Color(0.25f, 0.35f, 0.55f, 1f);

            _avatarInitials = UIFactory.CreateText(avatarFrame.transform, "AvatarInitials", "??", 24, TextAnchor.MiddleCenter);
            Stretch(_avatarInitials.rectTransform);

            var uploadBtn = UIFactory.CreateButton(
                _panelRoot.transform,
                "UploadPhoto",
                "Upload Photo",
                new Color(0.25f, 0.35f, 0.5f));
            var uploadRect = uploadBtn.GetComponent<RectTransform>();
            uploadRect.anchorMin = new Vector2(0.04f, 0.915f);
            uploadRect.anchorMax = new Vector2(0.04f, 0.915f);
            uploadRect.pivot = new Vector2(0f, 1f);
            uploadRect.sizeDelta = new Vector2(112f, 28f);
            uploadRect.anchoredPosition = new Vector2(0f, -118f);
            uploadBtn.onClick.AddListener(PickAndUploadPhoto);

            _profileNumberText = UIFactory.CreateText(
                _panelRoot.transform,
                "ProfileNumber",
                "Profile #: ---",
                14,
                TextAnchor.UpperLeft);
            SetRect(_profileNumberText.rectTransform, 0.25f, 0.66f, 0.97f, 0.70f);
            _profileNumberText.fontStyle = FontStyle.Bold;

            _memberSinceText = UIFactory.CreateText(
                _panelRoot.transform,
                "MemberSince",
                "Signed up: ---",
                14,
                TextAnchor.UpperLeft);
            SetRect(_memberSinceText.rectTransform, 0.25f, 0.62f, 0.97f, 0.66f);

            _statsText = UIFactory.CreateText(_panelRoot.transform, "Stats", "", 14, TextAnchor.UpperLeft);
            SetRect(_statsText.rectTransform, 0.25f, 0.70f, 0.97f, 0.915f);

            CreateLabel(_panelRoot.transform, "MoodLabel", "Mood", 0.60f, 0.64f);
            _moodInput = CreateInput(_panelRoot.transform, "MoodInput", "Ready to mine.", 0.54f, 0.59f, false);

            CreateLabel(_panelRoot.transform, "AboutLabel", "About Me", 0.50f, 0.54f);
            _aboutInput = CreateInput(_panelRoot.transform, "AboutInput", "", 0.34f, 0.48f, true);

            CreateLabel(_panelRoot.transform, "InterestsLabel", "Interests", 0.30f, 0.34f);
            _interestsInput = CreateInput(_panelRoot.transform, "InterestsInput", "", 0.18f, 0.28f, true);

            CreateLabel(_panelRoot.transform, "MusicLabel", "Now Playing", 0.14f, 0.18f);
            _musicInput = CreateInput(_panelRoot.transform, "MusicInput", "", 0.08f, 0.12f, false);

            _statusText = UIFactory.CreateText(_panelRoot.transform, "ProfileStatus", "", 13, TextAnchor.MiddleLeft);
            SetRect(_statusText.rectTransform, 0.04f, 0.02f, 0.96f, 0.07f);
            _statusText.color = new Color(0.75f, 0.9f, 1f);
        }

        public async void SetVisible(bool visible)
        {
            _visible = visible;
            _overlayRoot.SetActive(visible);
            _statusText.text = string.Empty;

            if (!visible)
            {
                return;
            }

            UIPopupFront.BringToFront(_overlayRoot.transform);

            try
            {
                await RefreshAsync();
            }
            catch (ApiException ex)
            {
                _statusText.text = ex.Message;
                _statusText.color = new Color(1f, 0.5f, 0.5f);
            }
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            var profile = await _session.LoadProfileAsync();
            _headerText.text = "My Profile";
            _profileNumberText.text = $"Profile #: {profile.profileNumber ?? "---"}";
            _memberSinceText.text = $"Signed up: {FormatDate(profile.memberSince)}";
            _statsText.text =
                $"{profile.username}\n" +
                $"Mine: {profile.mineName}\n" +
                $"Day {profile.currentGameDay}  ·  {CurrencyFormat.FormatAmount(profile.credits)}\n" +
                $"Workers {profile.workerCount}  ·  Zones {profile.zoneCount}";

            _moodInput.text = profile.mood ?? string.Empty;
            _aboutInput.text = profile.aboutMe ?? string.Empty;
            _musicInput.text = profile.music ?? string.Empty;
            _interestsInput.text = profile.interests ?? string.Empty;
            _avatarInitials.text = ProfileInitials(profile.username);
            await LoadAvatarAsync(profile.profileImageUrl);
        }

        private async System.Threading.Tasks.Task SaveProfileAsync()
        {
            _statusText.text = "Saving...";
            _statusText.color = new Color(0.75f, 0.9f, 1f);

            try
            {
                var request = new UpdatePlayerProfileRequest
                {
                    mood = _moodInput.text.Trim(),
                    aboutMe = _aboutInput.text,
                    music = _musicInput.text.Trim(),
                    interests = _interestsInput.text
                };

                await _session.UpdateProfileAsync(request);
                await RefreshAsync();
                _statusText.text = "Profile saved.";
                _statusText.color = new Color(0.6f, 1f, 0.7f);
            }
            catch (ApiException ex)
            {
                _statusText.text = ex.Message;
                _statusText.color = new Color(1f, 0.5f, 0.5f);
            }
        }

        private void PickAndUploadPhoto()
        {
#if UNITY_EDITOR
            var path = EditorUtility.OpenFilePanel("Profile photo", "", "png,jpg,jpeg,webp,gif");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            _ = UploadPhotoFromPathAsync(path);
#else
            _statusText.text = "Use the web client to upload a profile photo.";
            _statusText.color = new Color(1f, 0.75f, 0.5f);
#endif
        }

        private async System.Threading.Tasks.Task UploadPhotoFromPathAsync(string path)
        {
            _statusText.text = "Uploading photo...";
            _statusText.color = new Color(0.75f, 0.9f, 1f);

            try
            {
                var bytes = await File.ReadAllBytesAsync(path);
                var fileName = Path.GetFileName(path);
                var contentType = ContentTypeForExtension(Path.GetExtension(path));
                await _session.UploadProfileAvatarAsync(bytes, fileName, contentType);
                await RefreshAsync();
                _statusText.text = "Profile photo updated.";
                _statusText.color = new Color(0.6f, 1f, 0.7f);
            }
            catch (ApiException ex)
            {
                _statusText.text = ex.Message;
                _statusText.color = new Color(1f, 0.5f, 0.5f);
            }
        }

        private async System.Threading.Tasks.Task LoadAvatarAsync(string profileImageUrl)
        {
            if (string.IsNullOrWhiteSpace(profileImageUrl))
            {
                _avatarImage.texture = null;
                _avatarImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                _avatarImage.color = new Color(0.25f, 0.35f, 0.55f, 1f);
                _avatarInitials.gameObject.SetActive(true);
                return;
            }

            var url = _session.Api.BaseUrl.TrimEnd('/') + profileImageUrl;
            using var request = UnityWebRequestTexture.GetTexture(url);
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await System.Threading.Tasks.Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                _avatarImage.texture = null;
                _avatarImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                _avatarImage.color = new Color(0.25f, 0.35f, 0.55f, 1f);
                _avatarInitials.gameObject.SetActive(true);
                return;
            }

            var texture = DownloadHandlerTexture.GetContent(request);
            _avatarImage.texture = texture;
            _avatarImage.uvRect = GetCoverUvRect(texture.width, texture.height);
            _avatarImage.color = Color.white;
            _avatarInitials.gameObject.SetActive(false);
        }

        private static Rect GetCoverUvRect(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            var sourceAspect = (float)width / height;
            if (sourceAspect > 1f)
            {
                var visibleWidth = 1f / sourceAspect;
                return new Rect((1f - visibleWidth) * 0.5f, 0f, visibleWidth, 1f);
            }

            var visibleHeight = sourceAspect;
            return new Rect(0f, (1f - visibleHeight) * 0.5f, 1f, visibleHeight);
        }

        private static string ProfileInitials(string username)
        {
            var parts = (username ?? string.Empty).Trim().Split(' ');
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
            {
                return "??";
            }

            if (parts.Length == 1)
            {
                return parts[0].Length >= 2
                    ? parts[0][..2].ToUpperInvariant()
                    : parts[0].ToUpperInvariant();
            }

            return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
        }

        private static string ContentTypeForExtension(string extension) =>
            extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };

        private static string FormatDate(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "---";
            }

            if (System.DateTime.TryParse(value, out var parsed))
            {
                return parsed.ToString("MMMM d, yyyy");
            }

            return value;
        }

        private static void CreateLabel(Transform parent, string name, string text, float yMin, float yMax)
        {
            var label = UIFactory.CreateText(parent, name, text, 14, TextAnchor.LowerLeft);
            SetRect(label.rectTransform, 0.04f, yMin, 0.96f, yMax);
        }

        private static InputField CreateInput(
            Transform parent,
            string name,
            string placeholder,
            float yMin,
            float yMax,
            bool multiline)
        {
            var input = UIFactory.CreateInputField(parent, placeholder);
            input.name = name;
            input.lineType = multiline
                ? InputField.LineType.MultiLineNewline
                : InputField.LineType.SingleLine;
            SetRect(input.GetComponent<RectTransform>(), 0.04f, yMin, 0.96f, yMax);
            return input;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetSquareRect(RectTransform rect, float anchorX, float anchorY, float sizePx)
        {
            rect.anchorMin = new Vector2(anchorX, anchorY);
            rect.anchorMax = new Vector2(anchorX, anchorY);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(sizePx, sizePx);
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
