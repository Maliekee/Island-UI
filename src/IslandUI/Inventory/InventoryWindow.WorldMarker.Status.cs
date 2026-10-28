using IslandUI.UI;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    internal static partial class InventoryWindow
    {
        private static Image _markerStatusFooter, _markerStatusHeader;
        private static SheetRow _markerStatus;
        // attribute points to spend: the cerulean tag with its number, at the row's
        // right end, where the tracks end (the target indicator shows the level-up icon with
        // the number in it, not the bare arrow); the roster row keeps the bare arrow
        private static PointsTag _markerPoints;
        private const float MarkerTagH = PointsTag.DockPill;

        private static void EnsureMarkerStatus()
        {
            // the row is the last line of the marker's words column (PaintMarkerStatus);
            // the first pass's separate header and footer plates are no longer built
            _markerStatus = new SheetRow { Go = Owned.New("StatusIcons"), IconsOnly = true, Bare = true };
            _markerStatus.Go.transform.SetParent(_worldMarker.transform, false);
            EnsureRowExtras(_markerStatus, new SheetLine { Kind = LineStrip });
            foreach (Image separator in _markerStatus.Seps)
            {
                separator.sprite = null;
                separator.type = Image.Type.Simple;
                separator.preserveAspect = false;
            }
            _markerPoints = PointsTag.Create(_markerStatus.Go.transform, "Points", true);
            _markerStatus.Go.SetActive(false);
        }

        // THE ROW: food, water | mood, love, libido | pregnancy, each filled bottom up
        // as the roster fills them, in painted icons (make_anime_hud.py: st_*): the
        // painting rises over the same silhouette dark, inside the cream rim the
        // player panel's food and water wear. Mood keeps its colour and love its pink
        // as tints over the painted greys; the rest carry their own colours.
        private const float StatusIcon = 10.5f, StatusIconGap = 2.6f, StatusGroupGap = 6.5f;
        private static readonly string[] StatusArt = { "st_food", "st_water", "st_smile", "st_heart", "st_flame", "st_baby" };
        private static readonly float[] StatusInk = { .61f, .62f, .84f, .88f, .72f, .83f };   // each icon's ink across its box
        private static readonly bool[] StatusTinted = { false, false, true, true, false, false };

        private static float StatusSpan(int icons, bool level, float levelW)
        {
            float span = 0f;
            for (int i = 0; i < icons; i++)
            {
                if (i > 0) { span += i == 2 || i == 5 ? StatusGroupGap : StatusIconGap; }
                span += StatusIcon * StatusInk[i];
            }
            return level ? span + StatusGroupGap + levelW : span;
        }

        // ONE OF YOURS' PLAQUE (Forgejo #51): the row is the words column's last
        // line, under the tracks, and the card stands the plaque's full height
        // beside the column - name, kind, tracks and row on one left edge. The row
        // was a footer centred across the whole plate, under two columns, and lined
        // up with neither.
        private const float MarkerStatusH = 51f;        // the plaque with the row
        private const float MarkerStatusCard = 46f;     // its card, at most
        private const float StatusRowTop = 37.4f;       // under the stun track's 35.3

        // the plate's width the row asks for: the column's edge, the icons, and the
        // points tag ending where the tracks end
        private static float MarkerStatusWidth()
        {
            bool baby = _unitLine.Fracs != null && _unitLine.Fracs.Length > 7 && _unitLine.Fracs[7] >= 0f;
            bool level = _unitLine.LevelUp && _markerPoints != null;
            float levelW = level ? _markerPoints.Width : 0f;
            return EmblemPad + StatusSpan(baby ? StripIcons : StripIcons - 1, level, levelW) + MarkerRightPad;
        }

        private static void PaintMarkerStatus(float width, bool show)
        {
            if (_markerStatus.Go.activeSelf != show) { _markerStatus.Go.SetActive(show); }
            if (!show) { return; }
            bool baby = _unitLine.Fracs[7] >= 0f;
            int visibleIcons = baby ? StripIcons : StripIcons - 1;
            bool level = _unitLine.LevelUp && _markerPoints != null;
            float levelW = 0f;
            if (level)
            {
                _markerPoints.Show(PointsTag.Kind.Attribute, _unitLine.LevelPoints.ToString(), MarkerTagH);
                levelW = _markerPoints.Width;
            }
            else if (_markerPoints != null)
            {
                _markerPoints.Hide();
            }
            // the row from the words' edge, the points tag ending with the tracks
            Theme.Place(_markerStatus.Go.transform, 0f, 0f, width, MarkerStatusH);
            float centerY = StatusRowTop + StatusIcon * .5f;
            float x = EmblemPad;
            const float separatorWidth = .6f, separatorHeight = 8.5f;
            int sep = 0;
            for (int i = 0; i < StripIcons; i++)
            {
                if (i > 0)
                {
                    bool group = i == 2 || i == 5;
                    if (group)
                    {
                        Theme.Place(_markerStatus.Seps[sep++], x + (StatusGroupGap - separatorWidth) * .5f,
                            centerY - separatorHeight * .5f, separatorWidth, separatorHeight);
                        _markerStatus.Seps[sep - 1].color = new Color32(214, 170, 96, 200);
                    }
                    x += group ? StatusGroupGap : StatusIconGap;
                }
                float s = StatusIcon;
                float ink = s * StatusInk[i];
                float ix = x - (s - ink) * .5f, iy = centerY - s * .5f;
                Theme.Place(_markerStatus.IconBacks[i], ix, iy, s, s);
                Theme.Place(_markerStatus.IconFills[i], ix, iy, s, s);
                x += ink;
            }
            if (level)
            {
                Theme.Place(_markerPoints.Rect, width - MarkerRightPad - levelW, centerY - MarkerTagH * .5f, levelW, MarkerTagH);
            }
            _markerStatus.DrewMax = _unitLine.Max;
            _markerStatus.DrewBaby = baby;
            ShowStrip(_markerStatus, true);
            PaintMarkerIcons(_unitLine);
        }

        // the strip's values on the painted icons (PaintStrip's semantics, its art
        // swapped): the face and the heart follow the strip's choice of kit sprite
        private static void PaintMarkerIcons(SheetLine line)
        {
            for (int i = 0; i < StripIcons; i++)
            {
                Image fill = _markerStatus.IconFills[i], back = _markerStatus.IconBacks[i];
                _markerStatus.IconRims[i].enabled = false;
                if (!fill.gameObject.activeSelf) { continue; }
                string art = StatusArt[i];
                Sprite kit = line.Icons != null && i < line.Icons.Length ? line.Icons[i] : null;
                if (i == 2 && kit != null && kit == Theme.Kit("kit_frown", 0)) { art = "st_frown"; }
                if (i == 3 && kit != null && kit == Theme.Kit("kit_heart_broken", 0)) { art = "st_heart_broken"; }
                Sprite full = AnimePieces.Get(art), empty = AnimePieces.Get(art + "_empty");
                if (fill.sprite != full) { fill.sprite = full; }
                if (back.sprite != empty) { back.sprite = empty; }
                fill.preserveAspect = back.preserveAspect = false;
                fill.fillAmount = Mathf.Clamp01(line.Fracs[2 + i]);
                Color tint = StatusTinted[i] ? line.Tints[2 + i] : Color.white;
                tint.a = 1f;
                if (fill.color != tint) { fill.color = tint; }
                if (back.color != Color.white) { back.color = Color.white; }
            }
            if (_markerStatus.LoveMid != null && _markerStatus.LoveMid.gameObject.activeSelf) { _markerStatus.LoveMid.gameObject.SetActive(false); }
            if (_markerStatus.LoveTop != null && _markerStatus.LoveTop.gameObject.activeSelf) { _markerStatus.LoveTop.gameObject.SetActive(false); }
            _markerStatus.LevelTip = line.LevelTip;
        }

        private static string MarkerStatusTip()
        {
            if (_markerStatus == null || !_markerStatus.Go.activeInHierarchy
                || _unitLine.IconTips == null) { return null; }
            if (_markerPoints != null && _markerPoints.Rect.gameObject.activeInHierarchy && !string.IsNullOrEmpty(_unitLine.LevelTip)
                && RectTransformUtility.RectangleContainsScreenPoint(_markerPoints.Rect, Input.mousePosition, null))
            {
                return _unitLine.LevelTip;
            }
            for (int i = 0; i < StripIcons && i < _unitLine.IconTips.Length; i++)
            {
                Image icon = _markerStatus.IconBacks[i];
                if (icon.gameObject.activeInHierarchy
                    && RectTransformUtility.RectangleContainsScreenPoint(icon.rectTransform, Input.mousePosition, null))
                {
                    return _unitLine.IconTips[i];
                }
            }
            return null;
        }
    }
}
