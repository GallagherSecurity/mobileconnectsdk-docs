using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Com.Gallagher.Security.Mobileaccess;

namespace mobileconnectsdksample_xamarin
{
    [Application]
    class Application : Android.App.Application
    {
        // Notification channel IDs for android 8+
        readonly string unlockNotificationChannelId = "com.gallagher.mobileconnectsdksample.UnlockNotificationChannelId";
        readonly string foregroundNotificationChannelId = "com.gallagher.mobileconnectsdksample.ForegroundNotificationChannelId";

        public Application(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        { }

        public override void OnCreate()
        {
            base.OnCreate();

            IMobileAccess mobileAccess = MobileAccessProvider.Configure(this, null, unlockNotificationChannelId, foregroundNotificationChannelId);
            ConfigureMobileAccess(mobileAccess);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                ConfigureNotificationChannels();
            }
        }

        //@RequiresApi(Build.VERSION_CODES.O)
        private void ConfigureNotificationChannels()
        {
            NotificationChannel unlockNotificationChannel = new NotificationChannel(unlockNotificationChannelId,
                    "Unlock Notifications",
                    NotificationImportance.High);

            unlockNotificationChannel.EnableVibration(true);
            unlockNotificationChannel.EnableLights(true);
            unlockNotificationChannel.LockscreenVisibility = NotificationVisibility.Public;
            unlockNotificationChannel.SetSound(Android.Provider.Settings.System.DefaultNotificationUri,
                    new AudioAttributes.Builder()
                            .SetUsage(AudioUsageKind.NotificationEvent)
                            .SetContentType(AudioContentType.Sonification)
                            .Build());

            unlockNotificationChannel.Description = "Unlock Device Notifications";

            NotificationManager notificationManager = (NotificationManager)GetSystemService(NotificationService);
            if (notificationManager == null)
            {
                throw new Exception("Unable to get NotificationManager");
            }
            notificationManager.CreateNotificationChannel(unlockNotificationChannel);

            NotificationChannel foregroundNotificationChannel = new NotificationChannel(foregroundNotificationChannelId,
                    "Foreground Notification",
                    NotificationImportance.High);

            foregroundNotificationChannel.SetShowBadge(false);
            foregroundNotificationChannel.Importance = NotificationImportance.Min;
            foregroundNotificationChannel.Description = "Foreground Notifications";

            notificationManager.CreateNotificationChannel(foregroundNotificationChannel);
        }

        private void ConfigureMobileAccess(IMobileAccess mobileAccess)
        {
            // *********************************************************************************
            // Configure the SDK
            // *********************************************************************************

            // The default BluetoothBackgroundScanMode is FOREGROUND_ONLY
            mobileAccess.SetBluetoothBackgroundScanMode(BluetoothScanMode.BackgroundLowLatency);

            // The default IsNfcPreferred is true
            mobileAccess.SetIsNfcPreferred(true);

            // Automatic access is disabled by default. Call disableAutomaticAccess to turn it off if you turn it on
            mobileAccess.EnableAutomaticAccess();

            // more code will go here
            mobileAccess.StartScanning();
        }


    }

}