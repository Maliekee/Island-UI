using IslandUI.UI;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    internal static partial class InventoryWindow
    {
        private static Image _markerStatusFooter, _markerStatusHeader;
        private static SheetRow _markerStatus;
        // attribute points to spend: the cerulean tag with its number, a group of its
        // own after the last emoji (the target indicator shows the level-up icon with
        // the number in it, not the bare arrow); the roster row keeps the bare arrow
        private static PointsTag _markerPoints;
        private static Image _markerPointsSep;
        private const float MarkerTagH = 9f;
        private const float MarkerFooterTop = 26f;
        private const float MarkerFooterHeight = 27f;
        private const float MarkerFooterExtra = MarkerFooterTop + MarkerFooterHeight - 38f;

        private static void EnsureMarkerStatus()
        {
            _markerStatusFooter = Theme.KitImage(_worldMarker.transform, "StatusFooter", "kit_emblem_status_footer", 20, Color.white);
            _markerStatusFooter.transform.SetAsFirstSibling();
            // A separate header draws OVER the rising footer. This preserves
            // the upper rim while the footer fills behind its lower corners.
            _markerStatusHeader = Theme.KitImage(_worldMarker.transform, "StatusHeader", "kit_emblem_status_header", 20, Color.white);
            _markerStatusHeader.transform.SetSiblingIndex(1);
            _markerStatus = new SheetRow { Go = Owned.New("StatusIcons"), IconsOnly = true, Bare = true };
            _markerStatus.Go.transform.SetParent(_markerStatusFooter.transform, false);
            EnsureRowExtras(_markerStatus, new SheetLine { Kind = LineStrip });
            foreach (Image separator in _markerStatus.Seps)
            {
                separator.sprite = null;
                separator.type = Image.Type.Simple;
                separator.preserveAspect = false;
            }
            _markerPointsSep = PlainImage(_markerStatus.Go.transform, "PointsSep", MarkerMuted);
            _markerPoints = PointsTag.Create(_markerStatus.Go.transform, "Points");
            _markerStatusFooter.gameObject.SetActive(false);
            _markerStatusHeader.gameObject.SetActive(false);
        }

        // Selected footer A: food/water | mood/love/libido | pregnancy, in the
        // roster's own art and fill semantics. The full silhouette buys the room
        // beneath the medallion without widening the nameplate or shrinking icons.
        private static void PaintMarkerStatus(float width, bool show)
        {
            _markerStatusFooter.gameObject.SetActive(show);
            _markerStatusHeader.gameObject.SetActive(show);
            if (!show) { return; }
            const float size = 10.5f, iconGap = 3f, groupGap = 7f, separatorWidth = .8f;
            const float separatorHeight = 9.5f, edgePad = 5.5f;
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
            if (_markerPointsSep != null && _markerPointsSep.gameObject.activeSelf != level)
            {
                _markerPointsSep.gameObject.SetActive(level);
            }
            // Fit the footer to the visible ink, with equal margins. Its left
            // edge and emoji positions stay fixed; pregnancy only extends right.
            float span = 0f;
            for (int i = 0; i < visibleIcons; i++)
            {
                if (i > 0) { span += i == 2 || i == 5 ? groupGap : iconGap; }
                span += size * StripIconScale[i] * StripIconInk[i];
            }
            if (level)
            {
                span += groupGap + levelW;
            }
            float footerWidth = span + edgePad * 2f;
            // Keep the first emoji at x=4 while adding wood on both sides.
            Theme.Place(_markerStatusFooter, 4f - edgePad, MarkerFooterTop, footerWidth, MarkerFooterHeight);
            Theme.Place(_markerStatusHeader, 0f, 0f, width, 38f);
            // Center in the exposed wood below the header, excluding the bottom
            // bevel. The part of the footer behind the header is not icon space.
            const float bandTop = 38f, bottomBevel = 2f;
            float bandHeight = MarkerFooterTop + MarkerFooterHeight - bottomBevel - bandTop;
            Theme.Place(_markerStatus.Go.transform, 0f, bandTop - MarkerFooterTop, footerWidth, bandHeight);
            float centerY = bandHeight * .5f;
            float x = edgePad;
            int sep = 0;
            for (int i = 0; i < StripIcons; i++)
            {
                if (i > 0)
                {
                    bool group = i == 2 || i == 5;
                    if (group)
                    {
                        Theme.Place(_markerStatus.Seps[sep++], x + (groupGap - separatorWidth) * .5f,
                            centerY - separatorHeight * .5f, separatorWidth, separatorHeight);
                        _markerStatus.Seps[sep - 1].color = MarkerMuted;
                    }
                    x += group ? groupGap : iconGap;
                }
                float s = size * StripIconScale[i];
                float inkWidth = s * StripIconInk[i];
                float ix = x - (s - inkWidth) * .5f;
                float iy = centerY - s * .5f;
                Theme.Place(_markerStatus.IconRims[i], ix - .6f, iy - .6f, s + 1.2f, s + 1.2f);
                Theme.Place(_markerStatus.IconBacks[i], ix, iy, s, s);
                Theme.Place(_markerStatus.IconFills[i], ix, iy, s, s);
                if (i == LoveIcon)
                {
                    Theme.Place(_markerStatus.LoveMid, ix, iy, s, s);
                    Theme.Place(_markerStatus.LoveTop, ix, iy, s, s);
                }
                x += inkWidth;
                if (i == visibleIcons - 1 && level)
                {
                    Theme.Place(_markerPointsSep, x + (groupGap - separatorWidth) * .5f,
                        centerY - separatorHeight * .5f, separatorWidth, separatorHeight);
                    Theme.Place(_markerPoints.Rect, x + groupGap, centerY - MarkerTagH * .5f, levelW, MarkerTagH);
                }
            }
            _markerStatus.DrewMax = _unitLine.Max;
            _markerStatus.DrewBaby = baby;
            ShowStrip(_markerStatus, true);
            PaintStrip(_markerStatus, _unitLine);
            // Strong ink contours keep the familiar status colors legible on
            // dark walnut, including the unfilled part of each gauge.
            for (int i = 0; i < StripIcons; i++)
            {
                _markerStatus.IconRims[i].enabled = true;
                _markerStatus.IconRims[i].color = EmblemInk;
            }
        }

        private static string MarkerStatusTip()
        {
            if (_markerStatusFooter == null || !_markerStatusFooter.gameObject.activeInHierarchy
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
