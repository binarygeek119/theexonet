using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rava.UI
{
    [RequireComponent(typeof(Dropdown))]
    public class DropdownListOverlay : MonoBehaviour
    {
        private static readonly List<DropdownListOverlay> Registered = new List<DropdownListOverlay>();

        private Dropdown _dropdown;
        private RectTransform _template;
        private Transform _originalParent;
        private Canvas _templateCanvas;
        private bool _wasOpen;

        private Vector2 _anchorMin;
        private Vector2 _anchorMax;
        private Vector2 _pivot;
        private Vector2 _sizeDelta;
        private Vector2 _anchoredPosition;
        private Vector3 _localScale;

        private bool IsOpen => _template != null && _template.gameObject.activeSelf;

        private void Awake()
        {
            _dropdown = GetComponent<Dropdown>();
            _template = _dropdown.template;
            if (_template == null)
            {
                return;
            }

            _originalParent = _template.parent;
            StoreTemplateTransform();

            _templateCanvas = _template.GetComponent<Canvas>();
            if (_templateCanvas == null)
            {
                _templateCanvas = _template.gameObject.AddComponent<Canvas>();
            }

            _templateCanvas.overrideSorting = true;

            if (_template.GetComponent<GraphicRaycaster>() == null)
            {
                _template.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void OnEnable()
        {
            Registered.Add(this);
        }

        private void OnDisable()
        {
            Registered.Remove(this);
        }

        private void Update()
        {
            if (!IsOpen || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (IsPointerOverDropdownControls())
            {
                return;
            }

            CloseDropdown();
        }

        private void LateUpdate()
        {
            if (_template == null)
            {
                return;
            }

            var isOpen = IsOpen;
            if (isOpen && !_wasOpen)
            {
                CloseOtherDropdowns();
            }

            if (isOpen)
            {
                BringTemplateToFront();
            }
            else if (_wasOpen)
            {
                RestoreTemplateParent();
            }

            _wasOpen = isOpen;
        }

        private void CloseOtherDropdowns()
        {
            for (var i = Registered.Count - 1; i >= 0; i--)
            {
                var overlay = Registered[i];
                if (overlay != null && overlay != this && overlay.IsOpen)
                {
                    overlay.CloseDropdown();
                }
            }
        }

        private void BringTemplateToFront()
        {
            var rootCanvas = _dropdown.GetComponentInParent<Canvas>()?.rootCanvas;
            if (rootCanvas == null)
            {
                return;
            }

            if (_template.parent != rootCanvas.transform)
            {
                _template.SetParent(rootCanvas.transform, true);
            }

            _templateCanvas.sortingOrder = short.MaxValue;
            _template.SetAsLastSibling();
        }

        private bool IsPointerOverDropdownControls()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var result in results)
            {
                var hit = result.gameObject.transform;
                if (hit.IsChildOf(_template) || hit.IsChildOf(_dropdown.transform))
                {
                    return true;
                }
            }

            return false;
        }

        private void CloseDropdown()
        {
            if (_dropdown == null)
            {
                return;
            }

            _dropdown.Hide();
        }

        private void RestoreTemplateParent()
        {
            if (_originalParent == null || _template.parent == _originalParent)
            {
                return;
            }

            _template.SetParent(_originalParent, false);
            RestoreTemplateTransform();
        }

        private void StoreTemplateTransform()
        {
            _anchorMin = _template.anchorMin;
            _anchorMax = _template.anchorMax;
            _pivot = _template.pivot;
            _sizeDelta = _template.sizeDelta;
            _anchoredPosition = _template.anchoredPosition;
            _localScale = _template.localScale;
        }

        private void RestoreTemplateTransform()
        {
            _template.anchorMin = _anchorMin;
            _template.anchorMax = _anchorMax;
            _template.pivot = _pivot;
            _template.sizeDelta = _sizeDelta;
            _template.anchoredPosition = _anchoredPosition;
            _template.localScale = _localScale;
        }
    }
}
