using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using PowerShot.Models;


namespace PowerShot.Utils
{
    internal static class OverlayRenderer
    {
        private const float FontSizeRatio = 0.022f;
        private const float PaddingRatio = 0.015f;
        private const float MinFontSize = 10f;
        private const float MaxFontSize = 56f;
        private const float MinPadding = 6f;
        private const float MaxPadding = 32f;

        private const string FontFamily = "Segoe UI";
        private const string DefaultPosition = "TopLeft";

        // Cached: hostname/IP rarely change within a session; DNS calls are slow.
        private static string _cachedSystemInfo;

        public static void Apply(Bitmap bmp, AppSettings settings, Rectangle bounds)
        {
            if (settings == null || !settings.OverlayEnabled) return;

            var overlays = new Dictionary<string, List<string>>();

            if (settings.EmbedSysInfo)
            {
                AddOverlay(overlays, settings.SysInfoPosition, GetSystemInfoString());
            }
            if (!string.IsNullOrWhiteSpace(settings.OverlayText))
            {
                AddOverlay(overlays, settings.OverlayTextPosition, settings.OverlayText);
            }

            if (overlays.Count == 0) return;

            float shortEdge = Math.Min(bounds.Width, bounds.Height);
            float fontSize = shortEdge * FontSizeRatio;
            if (fontSize < MinFontSize) fontSize = MinFontSize;
            if (fontSize > MaxFontSize) fontSize = MaxFontSize;

            float padding = shortEdge * PaddingRatio;
            if (padding < MinPadding) padding = MinPadding;
            if (padding > MaxPadding) padding = MaxPadding;

            using (Graphics g = Graphics.FromImage(bmp))
            using (Font font = new Font(FontFamily, fontSize, FontStyle.Bold))
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                foreach (var kvp in overlays)
                {
                    string text = string.Join("\n", kvp.Value);
                    DrawBlock(g, font, bgBrush, textBrush, bounds, kvp.Key, text, padding);
                }
            }
        }

        public static string GetSystemInfoString()
        {
            if (_cachedSystemInfo != null) return _cachedSystemInfo;
            try
            {
                string host = Dns.GetHostName();
                string domain = IPGlobalProperties.GetIPGlobalProperties().DomainName;
                string fqdn = string.IsNullOrEmpty(domain) ? host : host + "." + domain;
                string user = Environment.UserDomainName + "\\" + Environment.UserName;

                var ips = Dns.GetHostAddresses(host)
                    .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ip => ip.ToString())
                    .ToList();

                string ipStr = ips.Count > 0 ? string.Join(", ", ips.ToArray()) : "N/A";
                _cachedSystemInfo = string.Format("Host: {0} | User: {1} | IP: {2}", fqdn, user, ipStr);
                return _cachedSystemInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine("  [Error] システム情報の取得に失敗しました: " + ex.Message);
                _cachedSystemInfo = "System Info Error";
                return _cachedSystemInfo;
            }
        }

        private static void AddOverlay(Dictionary<string, List<string>> overlays, string position, string text)
        {
            string pos = string.IsNullOrEmpty(position) ? DefaultPosition : position;
            List<string> list;
            if (!overlays.TryGetValue(pos, out list))
            {
                list = new List<string>();
                overlays[pos] = list;
            }
            list.Add(text);
        }

        private static void DrawBlock(Graphics g, Font font, SolidBrush bgBrush, SolidBrush textBrush,
            Rectangle bounds, string position, string text, float padding)
        {
            SizeF textSize = g.MeasureString(text, font);
            PointF pt = GetPosition(textSize, bounds, position, padding);

            g.FillRectangle(bgBrush, pt.X, pt.Y, textSize.Width + padding * 2, textSize.Height + padding * 2);
            g.DrawString(text, font, textBrush, pt.X + padding, pt.Y + padding);
        }

        private static PointF GetPosition(SizeF textSize, Rectangle bounds, string position, float padding)
        {
            float rectW = textSize.Width + padding * 2;
            float rectH = textSize.Height + padding * 2;
            float x = bounds.X + padding;
            float y = bounds.Y + padding;

            if (position == "TopRight")
            {
                x = bounds.Right - rectW - padding;
            }
            else if (position == "BottomLeft")
            {
                y = bounds.Bottom - rectH - padding;
            }
            else if (position == "BottomRight")
            {
                x = bounds.Right - rectW - padding;
                y = bounds.Bottom - rectH - padding;
            }

            if (x < bounds.X) x = bounds.X;
            if (y < bounds.Y) y = bounds.Y;
            return new PointF(x, y);
        }
    }
}
