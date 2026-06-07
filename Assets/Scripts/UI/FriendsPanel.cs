using System.Text;
using Theexonet.Core.Dtos;
using Theexonet.Mining;
using Theexonet.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Theexonet.UI
{
    public class FriendsPanel : MonoBehaviour
    {
        private GameSession _session;
        private GameObject _panelRoot;
        private InputField _profileNumberInput;
        private Text _statusText;
        private Text _listText;
        private bool _visible;

        public bool IsVisible => _visible;

        public void Initialize(GameSession session, Transform parent)
        {
            _session = session;
            _panelRoot = UIFactory.CreatePanel(parent, "FriendsPanel", new Color(0.08f, 0.1f, 0.15f, 0.96f));
            var rect = _panelRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, 0.08f);
            rect.anchorMax = new Vector2(0.92f, 0.92f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _panelRoot.SetActive(false);

            var title = UIFactory.CreateText(_panelRoot.transform, "Title", "Friends", 22, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, 0.28f, 0.935f, 0.72f, 0.985f);

            var closeBtn = UIFactory.CreateButton(_panelRoot.transform, "CloseFriends", "Close", new Color(0.25f, 0.3f, 0.4f));
            SetRect(closeBtn.GetComponent<RectTransform>(), 0.76f, 0.935f, 0.97f, 0.985f);
            closeBtn.onClick.AddListener(() => SetVisible(false));

            CreateLabel(_panelRoot.transform, "AddLabel", "Add friend by profile number", 0.86f, 0.90f);

            _profileNumberInput = CreateInput(_panelRoot.transform, "FriendNumberInput", "!K7R-8842-9F3A", 0.80f, 0.86f);
            _profileNumberInput.onSubmit.AddListener(unused => { _ = AddFriendAsync(); });

            var addBtn = UIFactory.CreateButton(
                _panelRoot.transform,
                "AddFriendBtn",
                "Add Friend",
                new Color(0.2f, 0.45f, 0.3f));
            SetRect(addBtn.GetComponent<RectTransform>(), 0.04f, 0.74f, 0.28f, 0.79f);
            addBtn.onClick.AddListener(() => _ = AddFriendAsync());

            _statusText = UIFactory.CreateText(_panelRoot.transform, "FriendsStatus", "", 13, TextAnchor.UpperLeft);
            SetRect(_statusText.rectTransform, 0.04f, 0.68f, 0.96f, 0.74f);

            CreateLabel(_panelRoot.transform, "ListLabel", "Friends & Requests", 0.62f, 0.66f);

            _listText = UIFactory.CreateText(_panelRoot.transform, "FriendsList", "Open this panel to load friends.", 14, TextAnchor.UpperLeft);
            SetRect(_listText.rectTransform, 0.04f, 0.08f, 0.96f, 0.62f);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            _panelRoot.SetActive(visible);
            if (visible)
            {
                UIPopupFront.BringToFront(_panelRoot.transform);
                _ = RefreshAsync();
            }
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            _statusText.text = "Loading...";
            _statusText.color = new Color(0.75f, 0.9f, 1f);

            try
            {
                var friends = await _session.LoadFriendsAsync();
                _listText.text = BuildFriendsList(friends);
                _statusText.text = string.Empty;
            }
            catch (ApiException ex)
            {
                _statusText.text = ex.Message;
                _statusText.color = new Color(1f, 0.5f, 0.5f);
            }
        }

        private async System.Threading.Tasks.Task AddFriendAsync()
        {
            var profileNumber = _profileNumberInput.text.Trim();
            if (string.IsNullOrEmpty(profileNumber))
            {
                _statusText.text = "Enter a profile number like !K7R-8842-9F3A.";
                _statusText.color = new Color(1f, 0.75f, 0.5f);
                return;
            }

            _statusText.text = "Sending request...";
            _statusText.color = new Color(0.75f, 0.9f, 1f);

            try
            {
                var result = await _session.AddFriendAsync(profileNumber);
                _profileNumberInput.text = string.Empty;
                _statusText.text = result.message;
                _statusText.color = new Color(0.6f, 1f, 0.7f);
                await RefreshAsync();
            }
            catch (ApiException ex)
            {
                _statusText.text = ex.Message;
                _statusText.color = new Color(1f, 0.5f, 0.5f);
            }
        }

        private static string BuildFriendsList(FriendsListResponse friends)
        {
            var builder = new StringBuilder();

            AppendSection(builder, "My Friends", friends.friends, emptyText: "No friends yet.");
            AppendSection(builder, "Incoming Requests", friends.incomingRequests, emptyText: "No incoming requests.");
            AppendSection(builder, "Pending Sent", friends.outgoingRequests, emptyText: "No pending requests.");

            return builder.ToString().TrimEnd();
        }

        private static void AppendSection(
            StringBuilder builder,
            string title,
            FriendSummaryDto[] items,
            string emptyText)
        {
            builder.AppendLine(title);
            builder.AppendLine(new string('-', title.Length));

            if (items == null || items.Length == 0)
            {
                builder.AppendLine(emptyText);
            }
            else
            {
                foreach (var friend in items)
                {
                    builder.AppendLine($"{friend.username}  ·  {friend.profileNumber}");
                    if (!string.IsNullOrEmpty(friend.mood))
                    {
                        builder.AppendLine($"  {friend.mood}");
                    }
                }
            }

            builder.AppendLine();
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
            float yMax)
        {
            var container = new GameObject(name, typeof(RectTransform));
            container.transform.SetParent(parent, false);
            SetRect(container.GetComponent<RectTransform>(), 0.30f, yMin, 0.96f, yMax);

            var input = UIFactory.CreateInputField(container.transform, placeholder);
            UIFactory.Stretch(input.GetComponent<RectTransform>());
            return input;
        }

        private static void SetRect(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
