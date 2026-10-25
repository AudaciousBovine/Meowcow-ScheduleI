using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Money;
using MeowcowMirror.Contexts;
using PlayerAppearance = Il2CppScheduleOne.Avatar.Player.PlayerAppearance;

namespace MeowcowMirror.Costs
{
    internal enum PayMethod { Card, Cash }

    internal sealed class ReceiptLine
    {
        public Service Service;
        public string Description;
        public float Price;
        public PayMethod Method;
    }

    /// <summary>
    /// Tracks what has changed since the session started and what it costs. Nothing is charged
    /// until Pay(); lines appear and disappear as the player changes things.
    /// </summary>
    internal static class Receipt
    {
        internal static bool PreferCash;
        internal static event Action Changed;

        private static ContextDefinition _context;
        private static PlayerAppearance _start;
        private static readonly List<ReceiptLine> _lines = new List<ReceiptLine>();
        private static readonly Dictionary<string, PayMethod> _chosen = new Dictionary<string, PayMethod>();

        internal static IReadOnlyList<ReceiptLine> Lines => _lines;
        internal static bool Active => _context != null;
        internal static float CashTotal => _lines.Where(l => l.Method == PayMethod.Cash).Sum(l => l.Price);
        internal static float CardTotal => _lines.Where(l => l.Method == PayMethod.Card).Sum(l => l.Price);

        internal static void Begin(ContextDefinition context, PlayerAppearance start)
        {
            _context = context;
            _start = start.Clone();
            _chosen.Clear();
            _lines.Clear();
            PriceBook.ReloadOverrides();
            Changed?.Invoke();
        }

        internal static void End()
        {
            _context = null;
            _start = null;
            _lines.Clear();
            _chosen.Clear();
        }

        /// <summary>Rebuilds the lines from the current appearance. Keeps each service's chosen payment method.</summary>
        internal static void Refresh(PlayerAppearance current)
        {
            if (_context == null || current == null) return;

            _lines.Clear();
            foreach (var service in ServiceCatalog.All)
            {
                if (!service.AppearsIn(_context.Id)) continue;
                if (service.Key(current) == service.Key(_start)) continue;

                string value = service.PriceValue?.Invoke(current) ?? "";
                float price = PriceBook.GetPrice(_context.Id, service, value);

                if (!_chosen.TryGetValue(service.Id, out var method))
                {
                    method = DefaultMethod(price);
                    _chosen[service.Id] = method;
                }

                _lines.Add(new ReceiptLine { Service = service, Description = service.Describe(current), Price = price, Method = method });
            }
            Changed?.Invoke();
        }

        /// <summary>Clicking a line: Card ↔ Cash. Free lines have nothing to pay with.</summary>
        internal static void Cycle(ReceiptLine line)
        {
            if (line == null || line.Price <= 0f) return;
            line.Method = line.Method == PayMethod.Card ? PayMethod.Cash : PayMethod.Card;
            _chosen[line.Service.Id] = line.Method;
            Changed?.Invoke();
        }

        /// <summary>The line's X: put that service back to how it was when the session started.</summary>
        internal static void Revert(ReceiptLine line)
        {
            if (line == null || _start == null) return;
            CreatorHooks.RevertService(line.Service, _start);
        }

        internal static float Balance(PayMethod method)
        {
            var money = NetworkSingleton<MoneyManager>.Instance;
            if (money == null) return 0f;
            return method == PayMethod.Cash ? money.cashBalance : money.onlineBalance;
        }

        internal static bool CanPay(out string reason)
        {
            reason = null;
            if (CashTotal > Balance(PayMethod.Cash)) { reason = "Not enough cash"; return false; }
            if (CardTotal > Balance(PayMethod.Card)) { reason = "Not enough on your card"; return false; }
            return true;
        }

        /// <summary>Charges every line. Card uses vanilla's transaction, so it shows in the ledger like vanilla purchases.</summary>
        internal static void Pay()
        {
            var money = NetworkSingleton<MoneyManager>.Instance;
            if (money == null) return;

            float cash = CashTotal, card = CardTotal;
            if (cash > 0f) money.ChangeCashBalance(-cash, true, true);
            if (card > 0f)
            {
                string note = string.Join(", ", _lines.Where(l => l.Method == PayMethod.Card && l.Price > 0f).Select(l => l.Description));
                money.CreateOnlineTransaction("Character customization", -card, 1f, note);
            }
            MirrorMod.Log.Msg($"Paid {MoneyManager.FormatAmount(cash + card, false, false)} (cash {cash}, card {card})");
        }

        private static PayMethod DefaultMethod(float price)
        {
            var preferred = PreferCash ? PayMethod.Cash : PayMethod.Card;
            var other = PreferCash ? PayMethod.Card : PayMethod.Cash;
            if (Balance(preferred) >= price) return preferred;
            if (Balance(other) >= price) return other;
            return preferred;
        }
    }
}
