using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace MajorMode.App.Services;

/// <summary>
/// Объявляет текст через UIA Notification event от элемента статуса (фокус не трогается).
/// Если конкретная программа экранного доступа не озвучивает уведомления,
/// включите <see cref="UseLiveRegion"/>: тогда вместо уведомления поднимается LiveRegionChanged.
/// Одновременно оба механизма не используются, чтобы не было двойного озвучивания.
/// </summary>
public sealed class UiaAnnouncer(TextBlock liveElement) : IAnnouncer
{
    public bool UseLiveRegion { get; set; }

    public void Announce(string text, bool important)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        liveElement.Text = text;
        var peer = UIElementAutomationPeer.FromElement(liveElement) ?? UIElementAutomationPeer.CreatePeerForElement(liveElement);
        if (peer is null)
        {
            return;
        }

        if (UseLiveRegion)
        {
            AutomationProperties.SetLiveSetting(liveElement, important ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            return;
        }

        peer.RaiseNotificationEvent(
            AutomationNotificationKind.Other,
            important ? AutomationNotificationProcessing.ImportantMostRecent : AutomationNotificationProcessing.MostRecent,
            text,
            "MajorMode.Status");
    }
}
