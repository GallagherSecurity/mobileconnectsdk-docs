package com.gallagher.mobileconnectsdksample.mobileconnectsdksample;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Intent;
import android.media.AudioAttributes;
import android.os.Build;
import android.provider.Settings;
import androidx.annotation.RequiresApi;

import com.gallagher.security.mobileaccess.BluetoothScanMode;
import com.gallagher.security.mobileaccess.CloudTlsValidationMode;
import com.gallagher.security.mobileaccess.MobileAccess;
import com.gallagher.security.mobileaccess.MobileAccessProvider;
import com.gallagher.security.mobileaccess.NotificationsConfiguration;
import com.gallagher.security.mobileaccess.SdkFeature;

import org.slf4j.LoggerFactory;

import java.util.EnumSet;

import ch.qos.logback.classic.Level;
import ch.qos.logback.classic.LoggerContext;
import ch.qos.logback.classic.android.LogcatAppender;
import ch.qos.logback.classic.encoder.PatternLayoutEncoder;
import ch.qos.logback.core.util.StatusPrinter;

public class Application extends android.app.Application {

    // Notification channel IDs for android 8+
    String unlockNotificationChannelId = "com.gallagher.mobileconnectsdksample.UnlockNotificationChannelId";
    String foregroundNotificationChannelId = "com.gallagher.mobileconnectsdksample.ForegroundNotificationChannelId";

    @Override
    public void onCreate() {
        super.onCreate();

        // *********************************************************************************
        // Configure the Mobile Connect SDK before we start
        // *********************************************************************************

        // Configure an intent which opens our app when the user taps on a notification.
        // You can specify the activity you'd like to launch and any other flags here,
        // or use different intents for the different kinds of notification that the SDK may show
        Intent resultIntent = new Intent(this, MainActivity.class);
        resultIntent.addCategory(Intent.CATEGORY_LAUNCHER);
        resultIntent.setAction(Intent.ACTION_MAIN);
        resultIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        PendingIntent notificationTappedIntent = PendingIntent.getActivity(this, 0, resultIntent, PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);

        NotificationsConfiguration notificationsConfiguration = new NotificationsConfiguration(
                unlockNotificationChannelId,
                notificationTappedIntent,
                foregroundNotificationChannelId,
                notificationTappedIntent);

        MobileAccess mobileAccess = MobileAccessProvider.configure(
                this, // reference to android Application
                null, // databaseFilePath: supply null to use the default
                notificationsConfiguration, // notifications config, as above
                EnumSet.of(SdkFeature.SALTO, SdkFeature.DIGITAL_ID), // the sample app enables both Salto and Digital ID. If you don't want those you can use EnumSet.noneOf(SdkFeature.class)
                CloudTlsValidationMode.ANY_VALID_CERTIFICATE_REQUIRED,
                null);

        if(Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            configureNotificationChannels();
        }

        configureLogging((LoggerContext) LoggerFactory.getILoggerFactory());

        // *********************************************************************************
        // Now that the SDK is configured, tell it to start scanning for readers

        // The default BluetoothBackgroundScanMode is FOREGROUND_ONLY.
        // We enable background scanning for sample purposes, but you may not want that
        mobileAccess.setBluetoothBackgroundScanMode(BluetoothScanMode.BACKGROUND_LOW_LATENCY);

        // The default IsNfcPreferred is true, so this does nothing. It is here for sample purposes
        mobileAccess.setIsNfcPreferred(true);

        // Automatic access is disabled by default. Call setAutomaticAccessEnabled(false) to turn it off later if you would like
        mobileAccess.setAutomaticAccessEnabled(true);

        // now start scanning for readers
        mobileAccess.setScanning(true);
    }

    @RequiresApi(Build.VERSION_CODES.O)
    private void configureNotificationChannels() {
        NotificationChannel unlockNotificationChannel = new NotificationChannel(unlockNotificationChannelId,
                "Unlock Notifications",
                NotificationManager.IMPORTANCE_HIGH);

        unlockNotificationChannel.enableVibration(true);
        unlockNotificationChannel.enableLights(true);
        unlockNotificationChannel.setLockscreenVisibility(Notification.VISIBILITY_PUBLIC);
        unlockNotificationChannel.setSound(Settings.System.DEFAULT_NOTIFICATION_URI,
                new AudioAttributes.Builder()
                        .setUsage(AudioAttributes.USAGE_NOTIFICATION_EVENT)
                        .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                        .build());

        unlockNotificationChannel.setDescription("Unlock Device Notifications");

        NotificationManager notificationManager = getSystemService(NotificationManager.class);
        if (notificationManager == null) {
            throw new RuntimeException("Unable to get NotificationManager");
        }
        notificationManager.createNotificationChannel(unlockNotificationChannel);

        NotificationChannel foregroundNotificationChannel = new NotificationChannel(foregroundNotificationChannelId,
                "Foreground Notification",
                NotificationManager.IMPORTANCE_MIN);

        foregroundNotificationChannel.setShowBadge(false);
        foregroundNotificationChannel.setImportance(NotificationManager.IMPORTANCE_MIN);
        foregroundNotificationChannel.setDescription("Foreground Notifications");

        notificationManager.createNotificationChannel(foregroundNotificationChannel);
    }

    private void configureLogging(LoggerContext loggerContext) {
        loggerContext.reset();

        // setup LogcatAppender to write to the standard Android Logcat
        PatternLayoutEncoder encoder2 = new PatternLayoutEncoder();
        encoder2.setContext(loggerContext);
        encoder2.setPattern("[%thread] %msg%n");
        encoder2.start();

        PatternLayoutEncoder tagEncoder = new PatternLayoutEncoder();
        tagEncoder.setContext(loggerContext);
        tagEncoder.setPattern("%logger{0}");
        tagEncoder.start();

        LogcatAppender logcatAppender = new LogcatAppender();
        logcatAppender.setContext(loggerContext);
        logcatAppender.setTagEncoder(tagEncoder);
        logcatAppender.setEncoder(encoder2);
        logcatAppender.start();

        // add the newly created appenders to the root logger;
        // qualify Logger to disambiguate from org.slf4j.Logger
        ch.qos.logback.classic.Logger root = (ch.qos.logback.classic.Logger) LoggerFactory.getLogger(ch.qos.logback.classic.Logger.ROOT_LOGGER_NAME);
        root.addAppender(logcatAppender);

        // Only show errors from the SDK, even though the app itself is logging at debug level
        ((ch.qos.logback.classic.Logger)LoggerFactory.getLogger("com.gallagher.security")).setLevel(Level.ERROR);

        if (BuildConfig.DEBUG) {
            root.setLevel(Level.DEBUG);
        } else {
            root.setLevel(Level.INFO);
        }

        root.info("----- Application onCreate -----");
        root.info("App Version: {}, BuildType {}", BuildConfig.VERSION_NAME, BuildConfig.BUILD_TYPE);
        root.info("OS Version: {} ({}) - API {}", new Object[]{System.getProperty("os.version"), Build.VERSION.INCREMENTAL, Build.VERSION.SDK_INT});
        root.info("Device: {}, Model: {}, Product {}", new Object[]{Build.DEVICE, Build.MODEL, Build.PRODUCT});
        StatusPrinter.print(loggerContext);
    }
}
