#if IOS
using Foundation;
using Microsoft.Maui.ApplicationModel;
using UserNotifications;
using UIKit;

namespace Wiretap.Maui.Services;

public sealed partial class WiretapEntryPointService
{
    private const string NotificationId = "wiretap_entry_point";
    private const string OpenWiretapKey = "wiretap_open";
    private static bool _delegateInitialized;

    partial void ShowPlatform(int count)
    {
        EnsureNotificationDelegate();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            UIApplication.SharedApplication.ApplicationIconBadgeNumber = count;
        });

        var center = UNUserNotificationCenter.Current;
        center.GetNotificationSettings(settings =>
        {
            if (settings.AuthorizationStatus != UNAuthorizationStatus.Authorized)
                return;

            var content = new UNMutableNotificationContent
            {
                Title = "Wiretap",
                Body = count == 0
                    ? "No captured requests"
                    : $"{count} captured request{(count == 1 ? "" : "s")}",
                Badge = NSNumber.FromInt32(count)
            };
            content.UserInfo = NSDictionary.FromObjectAndKey(new NSString("1"), new NSString(OpenWiretapKey));

            var request = UNNotificationRequest.FromIdentifier(NotificationId, content, null);
            center.AddNotificationRequest(request, _ => { });
        });
    }

    partial void HidePlatform()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UIApplication.SharedApplication.ApplicationIconBadgeNumber = 0;
        });

        var center = UNUserNotificationCenter.Current;
        center.RemovePendingNotificationRequests(new[] { NotificationId });
        center.RemoveDeliveredNotifications(new[] { NotificationId });
    }

    private static void EnsureNotificationDelegate()
    {
        if (_delegateInitialized)
            return;

        _delegateInitialized = true;

        var center = UNUserNotificationCenter.Current;
        if (center.Delegate is WiretapNotificationDelegate)
            return;

        center.Delegate = new WiretapNotificationDelegate(center.Delegate);
    }

    private sealed class WiretapNotificationDelegate : UNUserNotificationCenterDelegate
    {
        private readonly IUNUserNotificationCenterDelegate? _inner;

        public WiretapNotificationDelegate(IUNUserNotificationCenterDelegate? inner)
        {
            _inner = inner;
        }

        public override void DidReceiveNotificationResponse(
            UNUserNotificationCenter center,
            UNNotificationResponse response,
            Action completionHandler)
        {
            try
            {
                if (IsWiretapNotification(response))
                    OpenInspectorFromNotification();
            }
            finally
            {
                if (_inner != null)
                    _inner.DidReceiveNotificationResponse(center, response, completionHandler);
                else
                    completionHandler();
            }
        }

        public override void WillPresentNotification(
            UNUserNotificationCenter center,
            UNNotification notification,
            Action<UNNotificationPresentationOptions> completionHandler)
        {
            if (_inner != null)
                _inner.WillPresentNotification(center, notification, completionHandler);
            else
                completionHandler(UNNotificationPresentationOptions.Badge);
        }

        private static bool IsWiretapNotification(UNNotificationResponse response)
        {
            var userInfo = response.Notification.Request.Content.UserInfo;
            return userInfo != null && userInfo.ContainsKey(new NSString(OpenWiretapKey));
        }
    }
}
#endif
