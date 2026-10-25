using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;
using Il2CppScheduleOne.Money;
using MeowcowMirror.Costs;
using CreatorUI = Il2CppScheduleOne.CharacterCreator.CharacterCreatorInterface;
using UObject = UnityEngine.Object;

namespace MeowcowMirror.Layout
{
    /// <summary>
    /// The receipt on the right edge: one line per changed service with its price and payment
    /// method (click to switch Card/Cash) and an X to revert it, a total, and the commit button.
    /// Built from clones of the main panel's pieces so it matches the frame.
    /// </summary>
    internal static class ReceiptPanel
    {
        private const float Width = 340f;
        private const float EdgeMargin = 40f;
        private const float Padding = 12f;
        private const float RowHeight = 34f;
        private const float TotalHeight = 64f;
        private const string CashColor = "#54E717";
        private const string CardColor = "#4CBFFF";
        private const string ErrorColor = "#C84B46";

        private static RectTransform _root;
        private static RectTransform _list;
        private static TextMeshProUGUI _total;
        private static Button _commit;
        private static TMP_FontAsset _font;
        private static float _fontSize;
        private static string _commitLabel = "Pay for service";
        private static readonly List<Row> _rows = new List<Row>();

        private sealed class Row
        {
            public GameObject Go;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Cost;
            public int Index;
        }

        internal static void Build(CreatorUI ui, RectTransform menu, RectTransform topbar, float barHeight, float panelY, float panelHeight)
        {
            _rows.Clear();
            Receipt.Changed -= Refresh;
            Receipt.Changed += Refresh;

            _font = ui._categoryLabel.font;
            _fontSize = ui._categoryLabel.fontSize * 0.72f;

            var rootGo = new GameObject("MirrorReceipt");
            _root = rootGo.AddComponent<RectTransform>();
            _root.SetParent(menu.parent, false);
            _root.anchorMin = new Vector2(1f, 0.5f);
            _root.anchorMax = new Vector2(1f, 0.5f);
            _root.pivot = new Vector2(1f, 0.5f);
            _root.sizeDelta = new Vector2(Width, panelHeight);
            _root.anchoredPosition = new Vector2(-EdgeMargin, panelY);

            // Background, header and footer: the same pieces as the main panel.
            var background = UObject.Instantiate(menu.Find("Background").gameObject, _root).GetComponent<RectTransform>();
            Stretch(background);

            var header = CloneEmpty(topbar, _root, "Header");
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, barHeight);
            header.anchoredPosition = Vector2.zero;

            var title = UObject.Instantiate(ui._categoryLabel.gameObject, header).GetComponent<TextMeshProUGUI>();
            title.text = "Receipt";
            title.alignment = TextAlignmentOptions.Left;
            Stretch(title.rectTransform, Padding, 0f, Padding, 0f);

            var footerGo = new GameObject("Footer");
            var footer = footerGo.AddComponent<RectTransform>();
            footer.SetParent(_root, false);
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(0f, barHeight);
            footer.anchoredPosition = Vector2.zero;
            var footerBg = CloneEmpty(topbar, footer, "Background");
            Stretch(footerBg);
            footerBg.localScale = new Vector3(1f, -1f, 1f); // rounded corners face down

            _commit = MirrorFrame.CloneButton(ui._doneButton, footer, _commitLabel);
            var commitRect = _commit.GetComponent<RectTransform>();
            commitRect.anchorMin = new Vector2(0f, 0.5f);
            commitRect.anchorMax = new Vector2(1f, 0.5f);
            commitRect.pivot = new Vector2(0.5f, 0.5f);
            commitRect.sizeDelta = new Vector2(-Padding * 2f, commitRect.sizeDelta.y);
            commitRect.anchoredPosition = Vector2.zero;
            _commit.onClick.AddListener(MirrorFrame.ToUnityAction(OnCommit));

            // Total, just above the footer.
            _total = MakeText(_root, "Total", TextAlignmentOptions.TopLeft);
            var totalRect = _total.rectTransform;
            totalRect.anchorMin = new Vector2(0f, 0f);
            totalRect.anchorMax = new Vector2(1f, 0f);
            totalRect.pivot = new Vector2(0.5f, 0f);
            totalRect.sizeDelta = new Vector2(-Padding * 2f, TotalHeight);
            totalRect.anchoredPosition = new Vector2(0f, barHeight + 6f);
            _total.richText = true;

            // Lines, between the header and the total.
            var listGo = new GameObject("Lines");
            _list = listGo.AddComponent<RectTransform>();
            _list.SetParent(_root, false);
            Stretch(_list, Padding, barHeight + TotalHeight + 12f, Padding, barHeight + Padding);
            var layout = listGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            rootGo.SetActive(false);
        }

        internal static void SetVisible(bool visible, string commitLabel)
        {
            if (_root == null) return;
            _commitLabel = string.IsNullOrWhiteSpace(commitLabel) ? "Pay for service" : commitLabel;
            _root.gameObject.SetActive(visible);
            if (visible) Refresh();
        }

        internal static bool Contains(Vector2 screenPoint) => PreviewControls.Contains(_root, screenPoint);

        // ---------- Content ----------

        private static void Refresh()
        {
            if (_root == null || !_root.gameObject.activeSelf) return;

            var lines = Receipt.Lines;
            while (_rows.Count < lines.Count) _rows.Add(CreateRow(_rows.Count));

            float cashBalance = Receipt.Balance(PayMethod.Cash);
            float cardBalance = Receipt.Balance(PayMethod.Card);
            bool cashShort = Receipt.CashTotal > cashBalance;
            bool cardShort = Receipt.CardTotal > cardBalance;

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                bool used = i < lines.Count;
                row.Go.SetActive(used);
                if (!used) continue;

                var line = lines[i];
                row.Label.text = line.Description;

                if (line.Price <= 0f)
                {
                    row.Cost.text = $"<color={CardColor}>Free</color>";
                }
                else
                {
                    bool cash = line.Method == PayMethod.Cash;
                    bool shortOfIt = cash ? cashShort : cardShort;
                    string color = shortOfIt ? ErrorColor : (cash ? CashColor : CardColor);
                    row.Cost.text = $"<color={color}>{MoneyManager.FormatAmount(line.Price, false, false)} {(cash ? "Cash" : "Card")}</color>";
                }
            }

            bool canPay = Receipt.CanPay(out string reason);
            float total = Receipt.CashTotal + Receipt.CardTotal;

            if (lines.Count == 0)
            {
                _total.text = "No changes yet";
            }
            else if (total <= 0f)
            {
                _total.text = "Total: Free";
            }
            else
            {
                string text = $"Total: {MoneyManager.FormatAmount(total, false, false)}";
                var parts = new List<string>();
                if (Receipt.CardTotal > 0f) parts.Add($"<color={(cardShort ? ErrorColor : CardColor)}>Card {MoneyManager.FormatAmount(Receipt.CardTotal, false, false)}</color>");
                if (Receipt.CashTotal > 0f) parts.Add($"<color={(cashShort ? ErrorColor : CashColor)}>Cash {MoneyManager.FormatAmount(Receipt.CashTotal, false, false)}</color>");
                if (parts.Count > 0) text += "\n" + string.Join("   ", parts);
                if (!canPay) text += $"\n<color={ErrorColor}>{reason}</color>";
                _total.text = text;
            }

            // Nothing changed: the button just closes, for free.
            MirrorFrame.SetLabel(_commit, lines.Count == 0 ? "Done" : _commitLabel);
            _commit.interactable = lines.Count == 0 || canPay;
        }

        private static void OnCommit()
        {
            if (Receipt.Lines.Count > 0)
            {
                if (!Receipt.CanPay(out _)) return;
                Receipt.Pay();
            }
            MirrorFrame.Commit();
        }

        // ---------- Building blocks ----------

        private static Row CreateRow(int index)
        {
            var row = new Row { Index = index };

            row.Go = new GameObject("Line" + index);
            var rect = row.Go.AddComponent<RectTransform>();
            rect.SetParent(_list, false);
            rect.sizeDelta = new Vector2(0f, RowHeight);

            // The whole line is a button that switches the payment method.
            var image = row.Go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.06f);
            var cycle = row.Go.AddComponent<Button>();
            cycle.targetGraphic = image;
            cycle.onClick.AddListener(MirrorFrame.ToUnityAction(() => Receipt.Cycle(LineAt(row.Index))));

            row.Label = MakeText(rect, "Label", TextAlignmentOptions.MidlineLeft);
            Stretch(row.Label.rectTransform, 8f, 0f, 150f, 0f);

            row.Cost = MakeText(rect, "Cost", TextAlignmentOptions.MidlineRight);
            row.Cost.richText = true;
            var costRect = row.Cost.rectTransform;
            costRect.anchorMin = new Vector2(1f, 0f);
            costRect.anchorMax = new Vector2(1f, 1f);
            costRect.pivot = new Vector2(1f, 0.5f);
            costRect.sizeDelta = new Vector2(120f, 0f);
            costRect.anchoredPosition = new Vector2(-(RowHeight + 4f), 0f);

            // X: revert this change.
            var removeGo = new GameObject("Remove");
            var removeRect = removeGo.AddComponent<RectTransform>();
            removeRect.SetParent(rect, false);
            removeRect.anchorMin = new Vector2(1f, 0.5f);
            removeRect.anchorMax = new Vector2(1f, 0.5f);
            removeRect.pivot = new Vector2(1f, 0.5f);
            removeRect.sizeDelta = new Vector2(RowHeight - 6f, RowHeight - 6f);
            removeRect.anchoredPosition = new Vector2(-3f, 0f);
            var removeImage = removeGo.AddComponent<Image>();
            removeImage.color = new Color(0.78f, 0.29f, 0.27f, 1f);
            var remove = removeGo.AddComponent<Button>();
            remove.targetGraphic = removeImage;
            remove.onClick.AddListener(MirrorFrame.ToUnityAction(() => Receipt.Revert(LineAt(row.Index))));
            var x = MakeText(removeRect, "X", TextAlignmentOptions.Center);
            x.text = "X";
            Stretch(x.rectTransform);

            return row;
        }

        private static ReceiptLine LineAt(int index) =>
            index >= 0 && index < Receipt.Lines.Count ? Receipt.Lines[index] : null;

        private static TextMeshProUGUI MakeText(RectTransform parent, string name, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) text.font = _font;
            text.fontSize = _fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CloneEmpty(RectTransform source, Transform parent, string name)
        {
            var clone = UObject.Instantiate(source.gameObject, parent).GetComponent<RectTransform>();
            clone.name = name;
            for (int i = clone.childCount - 1; i >= 0; i--)
                UObject.DestroyImmediate(clone.GetChild(i).gameObject);
            return clone;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
