using IslandUI.UI;
using UnityEngine;
using UnityEngine.UI;

namespace IslandUI
{
    internal static partial class InventoryWindow
    {
        // ---------- a door between you and your target ----------

        // A HOUSE'S INTERIOR IS A ROOM 25 m UNDER IT (BuildManager.SetHouse), so
        // a person in there seen from outside - or outside, seen from in there -
        // stands where your screen means nothing: the arrow pointed underground,
        // or up into the sky. The marker then stands on the door between you,
        // with a tag under it - the house's glyph and "Inside" or "Outside" -
        // so it reads as "behind this door", not as the house being the
        // target. No ground ring; off the screen the arrow and the distance
        // lead to the door. The tag once had a pin down to the door's foot;
        // it read as a stray arrow (Forgejo #62).
        private static Image _doorPlate, _doorGlyph;
        private static TMPro.TextMeshProUGUI _doorText;
        private static bool _doorTagShown;
        private const float IndoorsDrop = 8f;      // metres: interiors are some 25 m down
        private const float DoorTagH = 15f;
        private const float DoorTagGap = 3f;
        private const float DoorTagExtra = DoorTagGap + DoorTagH;
        private const float DoorTagLift = 2.8f;    // metres over the door's foot: its lintel

        // The door between you, if one is: their house's, seen from outside, or
        // the way out of yours, when you are in a house and they are not.
        // NPCManager.HouseIn sets nMove.house for villagers and natives alike;
        // a thing in there is the house's by HouseOf.
        private static bool DoorBetween(CommonStates who, out Vector3 door, out bool inside)
        {
            return DoorBetween(who != null ? who.transform : null, who, null, out door, out inside);
        }

        private static bool DoorBetween(GameObject prop, out Vector3 door, out bool inside)
        {
            return DoorBetween(prop != null ? prop.transform : null, null, prop, out door, out inside);
        }

        private static bool DoorBetween(Transform target, CommonStates who, GameObject prop, out Vector3 door, out bool inside)
        {
            door = Vector3.zero;
            inside = false;
            GameObject player = _mn != null && _mn.gameMN != null ? _mn.gameMN.player : null;
            if (player == null || target == null)
            {
                return false;
            }
            float mine = player.transform.position.y, theirs = target.position.y;
            if (Mathf.Abs(mine - theirs) < IndoorsDrop)
            {
                return false;   // the same floor, in the same room or both outside
            }
            if (theirs < mine)
            {
                DoorNPC house = who != null ? (who.nMove != null ? who.nMove.house : null) : HouseOf(prop);
                if (house == null)
                {
                    return false;
                }
                door = house.door != null ? house.door.transform.position : house.transform.position;
                inside = true;
                return true;
            }
            Transform exit = WayOut(player.transform.position);
            if (exit == null)
            {
                return false;
            }
            door = exit.position;
            return true;
        }

        // the house a thing stands in: the interior is the house's child
        // (DoorNPC.interiorGrp), and what you build in there carries the
        // house's id (ItemInfo.houseID, BuildManager)
        private static DoorNPC HouseOf(GameObject prop)
        {
            if (prop == null)
            {
                return null;
            }
            DoorNPC house = prop.GetComponentInParent<DoorNPC>();
            if (house != null)
            {
                return house;
            }
            ItemInfo info = prop.GetComponent<ItemInfo>();
            BuildManager build = _mn != null ? _mn.buildMN : null;
            if (info == null || info.houseID == 0 || build == null || build.houses == null)
            {
                return null;
            }
            for (int i = 0; i < build.houses.Count; i++)
            {
                if (build.houses[i] != null && build.houses[i].houseID == info.houseID)
                {
                    return build.houses[i].GetComponent<DoorNPC>();
                }
            }
            return null;
        }

        // the inner door of the house you are in (DoorScript "in_door", the one
        // that sets GameManager.tmpHouse back to 0), found once per house
        private static int _wayOutFor;
        private static Transform _wayOut;

        private static Transform WayOut(Vector3 here)
        {
            int id = _mn.gameMN.tmpHouse;
            if (id <= 0)
            {
                return null;
            }
            if (_wayOutFor == id && _wayOut != null)
            {
                return _wayOut;
            }
            _wayOutFor = id;
            _wayOut = null;
            BuildManager build = _mn.buildMN;
            for (int i = 0; build != null && build.houses != null && i < build.houses.Count; i++)
            {
                if (build.houses[i] != null && build.houses[i].houseID == id)
                {
                    _wayOut = build.houses[i].transform.Find("in_door");
                    break;
                }
            }
            if (_wayOut == null)
            {
                // a house not in the build list: the nearest inner door
                float best = float.MaxValue;
                foreach (DoorScript d in Object.FindObjectsOfType<DoorScript>())
                {
                    if (d.name != "in_door") { continue; }
                    float far = (d.transform.position - here).sqrMagnitude;
                    if (far < best) { best = far; _wayOut = d.transform; }
                }
            }
            return _wayOut;
        }

        private static void EnsureDoorTag()
        {
            if (_doorPlate != null)
            {
                return;
            }
            _doorPlate = Theme.KitImage(_worldMarker.transform, "DoorTag", "kit_emblem_plate", 20, Color.white);
            _doorPlate.raycastTarget = false;
            _doorGlyph = Theme.KitImage(_doorPlate.transform, "Glyph", "kit_paper", 0, MarkerCream);
            _doorGlyph.sprite = HudGlyphs.Get("home");
            _doorGlyph.preserveAspect = true;
            _doorGlyph.raycastTarget = false;
            _doorText = Theme.MakeKitText(_doorPlate.transform, "Words", 8f, MarkerCream, TMPro.TextAlignmentOptions.MidlineLeft, true);
            SetMarkerFont(_doorText, true);
            _doorText.enableWordWrapping = false;
            _doorText.raycastTarget = false;
            _doorPlate.gameObject.SetActive(false);
        }

        // back to the plain marker: every other paint calls this first
        private static void DoorTagOff()
        {
            if (!_doorTagShown)
            {
                return;
            }
            _doorTagShown = false;
            _doorPlate.gameObject.SetActive(false);
        }

        // after PlaceWorldMarker, which kept DoorTagExtra free under the marker
        // (and under its status footer, footer high)
        private static void PaintDoorTag(bool inside, float width, float height, float footer)
        {
            EnsureDoorTag();
            _doorTagShown = true;
            // the marker over it already names them
            string words = inside ? Shared.Loc.Get("marker.door.inside", "Inside") : Shared.Loc.Get("marker.door.outside", "Outside");
            if (_doorText.text != words) { _doorText.text = words; }
            const float glyph = 9f, padL = 5f, gap = 3.5f, padR = 6f;
            float textW = Mathf.Ceil(_doorText.GetPreferredValues(words).x);
            float tagW = padL + glyph + gap + textW + padR;
            float tagY = height + footer + DoorTagGap;
            Theme.Place(_doorPlate, (width - tagW) * .5f, tagY, tagW, DoorTagH);
            Theme.Place(_doorGlyph, padL, (DoorTagH - glyph) * .5f, glyph, glyph);
            Theme.Place(_doorText, padL + glyph + gap, 0f, textW + 1f, DoorTagH);
            _doorPlate.gameObject.SetActive(true);
            _doorPlate.transform.SetAsLastSibling();
        }
    }
}
