package com.gallagher.mobileconnectsdksample.mobileconnectsdksample;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.media.AudioAttributes;
import android.os.Build;
import android.provider.Settings;
import androidx.annotation.RequiresApi;

import com.gallagher.security.mobileaccess.BluetoothScanMode;
import com.gallagher.security.mobileaccess.MobileAccess;
import com.gallagher.security.mobileaccess.MobileAccessProvider;

import org.slf4j.LoggerFactory;

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

        MobileAccess mobileAccess = MobileAccessProvider.configure(this, null, unlockNotificationChannelId, foregroundNotificationChannelId);
        configureMobileAccess(mobileAccess);

        if(Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            configureNotificationChannels();
        }

        configureLogging((LoggerContext) LoggerFactory.getILoggerFactory());
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

    private void configureMobileAccess(MobileAccess mobileAccess) {
        // *********************************************************************************
        // Configure the SDK
        // *********************************************************************************

        // The default BluetoothBackgroundScanMode is FOREGROUND_ONLY
        mobileAccess.setBluetoothBackgroundScanMode(BluetoothScanMode.BACKGROUND_LOW_LATENCY);

        // The default IsNfcPreferred is true
        mobileAccess.setIsNfcPreferred(true);

        // Automatic access is disabled by default. Call disableAutomaticAccess to turn it off if you turn it on
        mobileAccess.setAutomaticAccessEnabled(true);

        // more code will go here
        mobileAccess.setScanning(true);
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
