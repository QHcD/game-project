using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Lightweight keyboard navigation glue for runtime-built menus.
///
/// Unity's EventSystem already handles Arrow Keys / Enter (Submit) on any
/// <see cref="Selectable"/> as long as
///   1. Some selectable is the current EventSystem.selectedGameObject, and
///   2. Selectables have valid Navigation set up.
///
/// Runtime-built menus often miss those two prerequisites because we never
/// wire navigation explicitly. This helper makes both of them happen for an
/// arbitrary list of buttons/sliders/dropdowns/toggles in vertical or
/// horizontal arrangement.
///
/// Usage (call after building the menu):
///     MenuKeyboardNavigator.AttachVertical(canvasObj, buttons);
///     MenuKeyboardNavigator.AttachHorizontal(rowObj, options);
/// </summary>
public class MenuKeyboardNavigator : MonoBehaviour
{
    public enum NavAxis { Vertical, Horizontal }

    [SerializeField] private List<Selectable> _items = new List<Selectable>();
    [SerializeField] private NavAxis _axis           = NavAxis.Vertical;
    [SerializeField] private bool    _wrapAround     = true;

    private bool _restoreSelectionPending;

    /// <summary>Attach a vertical (Up/Down) navigator and return the component.</summary>
    public static MenuKeyboardNavigator AttachVertical(GameObject host, IList<Selectable> items, bool wrap = true)
        => Attach(host, items, NavAxis.Vertical, wrap);

    /// <summary>Attach a horizontal (Left/Right) navigator and return the component.</summary>
    public static MenuKeyboardNavigator AttachHorizontal(GameObject host, IList<Selectable> items, bool wrap = true)
        => Attach(host, items, NavAxis.Horizontal, wrap);

    private static MenuKeyboardNavigator Attach(GameObject host, IList<Selectable> items, NavAxis axis, bool wrap)
    {
        if (host == null) return null;
        MenuKeyboardNavigator nav = host.GetComponent<MenuKeyboardNavigator>();
        if (nav == null) nav = host.AddComponent<MenuKeyboardNavigator>();
        nav._items.Clear();
        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null) nav._items.Add(items[i]);
        }
        nav._axis = axis;
        nav._wrapAround = wrap;
        nav.WireNavigation();
        nav._restoreSelectionPending = true;
        return nav;
    }

    private void OnEnable()
    {
        _restoreSelectionPending = true;
    }

    private void Update()
    {
        if (!_restoreSelectionPending) return;
        if (_items == null || _items.Count == 0) return;

        EventSystem es = EventSystem.current;
        if (es == null) return;

        // If nothing is selected (or the selection is a stale/destroyed object,
        // or a non-interactable element from another menu), pick the first
        // interactable item in this navigator group.
        GameObject current = es.currentSelectedGameObject;
        bool needFreshSelect = current == null
            || !current.activeInHierarchy
            || !ItemListContains(current);

        if (needFreshSelect)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                Selectable item = _items[i];
                if (item == null || !item.IsInteractable() || !item.gameObject.activeInHierarchy) continue;
                es.SetSelectedGameObject(item.gameObject);
                _restoreSelectionPending = false;
                return;
            }
        }
        else
        {
            _restoreSelectionPending = false;
        }
    }

    private bool ItemListContains(GameObject go)
    {
        for (int i = 0; i < _items.Count; i++)
            if (_items[i] != null && _items[i].gameObject == go) return true;
        return false;
    }

    private void WireNavigation()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            Selectable item = _items[i];
            if (item == null) continue;

            Selectable prev = FindNeighbour(i, -1);
            Selectable next = FindNeighbour(i, +1);

            Navigation nav = new Navigation();
            nav.mode = Navigation.Mode.Explicit;
            if (_axis == NavAxis.Vertical)
            {
                nav.selectOnUp   = prev;
                nav.selectOnDown = next;
            }
            else
            {
                nav.selectOnLeft  = prev;
                nav.selectOnRight = next;
            }
            item.navigation = nav;
        }
    }

    private Selectable FindNeighbour(int index, int direction)
    {
        if (_items.Count == 0) return null;
        int n = _items.Count;
        int cursor = index;
        for (int step = 0; step < n; step++)
        {
            cursor += direction;
            if (_wrapAround)
                cursor = ((cursor % n) + n) % n;
            else if (cursor < 0 || cursor >= n)
                return null;

            Selectable candidate = _items[cursor];
            if (candidate != null && candidate != _items[index] && candidate.IsInteractable())
                return candidate;
            if (cursor == index) return null;
        }
        return null;
    }
}
