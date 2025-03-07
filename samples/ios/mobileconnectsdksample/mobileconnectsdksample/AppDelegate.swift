//
// Copyright Gallagher Group Ltd 2024 All Rights Reserved
//
import UIKit
import CoreLocation
import UserNotifications
import GallagherMobileAccess
import os.log

private let log = Log.createForClass(AppDelegate.self)

@UIApplicationMain
class AppDelegate: UIResponder, UIApplicationDelegate, CLLocationManagerDelegate, LogWriter {
    
    let mobileAccess: MobileAccess
    
    override init() {
        // *********************************************************************************
        // Configure the Mobile Connect SDK before we start
        // *********************************************************************************
        
        // Note to developer: If you want to customise messages generated by the SDK, pass a MobileAccessLocalization struct to configure here
        mobileAccess = MobileAccessProvider.configure(
            // the sample app enables Aperio, Salto, Digital ID and Wallet. If you don't want those you can use [] or simply don't supply enabledFeatures at all
            enabledFeatures: [.digitalId, .salto, .aperio, .wallet]
        )
        
        super.init()
        
        //Log configuration
        guard let documentsPath = NSSearchPathForDirectoriesInDomains(.documentDirectory, .userDomainMask, true).first else {
            fatalError("NSSearchPathForDirectoriesInDomains couldn't find user documents directory")
        }

        let logConfig = LogConfiguration(fileUrl: URL(fileURLWithPath: documentsPath).appendingPathComponent("GallagherMobileAccessSampleApp"),
                         rotationFileSize: 3000000,    //3 MB
                         rotationKeepCount: 5)

        Log.configuration = logConfig
        
        Log.singletonInstance.level = .verbose
        log.info("----- Application didFinishLaunchingWithOptions -----")
        log.info("App version: \(Utils.applicationVersion)")
        log.info("OS Version: \(Utils.systemVersion)")
        log.info("Device: \(UIDevice.current.modelName)")
        log.info("SDK Version: \(Utils.sdkFrameworkVersion)")
        
        
        // Configure crash logging
        NSSetUncaughtExceptionHandler { exception in
            log.error("CRASH: \(exception)")
            log.error("Stack Trace: \(exception.callStackSymbols)")
        }
    }

    var window: UIWindow?

    func application(_ application: UIApplication, didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?) -> Bool {
       
        // *********************************************************************************
        // Now that the SDK is configured, tell it to start scanning for readers
        
        // for sample purposes, extended background scanning is off by default. Uncomment the line below to enable it
        // enableExtendedBackgroundScanning()
        
        // Automatic access is disabled by default. Set isAutomaticAccessEnabled = false to turn it off later if you would like
        mobileAccess.isAutomaticAccessEnabled = true
        
        // now start scanning for readers
        mobileAccess.setScanning(enabled: true)
        
        return true
    }
    
    var level: LogLevel = .verbose
    
    // Configure the SDK to write to os_log. This isn't neccessary, just to demonstrate how you might do it
    func write(level: LogLevel, message: () -> String, parameters: LogParameters) {
        if #available(iOS 10.0, *) {
            let osLogType: OSLogType
            switch level { // map SDK log level to OSLogType
            case .fatal: osLogType = .fault
            case .error, .warn: osLogType = .error
            case .info: osLogType = .info
            case .debug, .verbose: osLogType = .debug
            @unknown default: osLogType = .debug // in case of future change
            }
            
            os_log("[%@] %@",
                   log: OSLog(subsystem: "com.gallagher.MobileConnectSdkSample", category: "mobileaccess"),
                   type: osLogType,
                   parameters.className ?? "",
                   message())
        } else {
            print(message())
        }
    }
    
    // we need to keep the locationManager around; If it goes out of scope while the permissions prompt
    // is on-screen it will auto close the prompt and the user won't be able to grant us authorization
    var locationManager: CLLocationManager?
    
    func enableExtendedBackgroundScanning() {
        let lm = CLLocationManager()
        locationManager = lm
        lm.delegate = self
        
        let authStatus = CLLocationManager.authorizationStatus()
        if authStatus == .authorizedAlways {
            mobileAccess.backgroundScanningMode = .extended
            // we haven't started scanning yet so don't need to restart it
        } else if authStatus == .notDetermined {
            lm.requestAlwaysAuthorization()
        }
        // else the user has explicitly denied us access, there's no point requesting authorisation as it will instantly fail
        // Instead we can prompt them to go and enable location permissions in the iOS settings app
        
        // PART 2:
        // ask to enable notifications (required to prompt the user to open the app for second factor in the background)
        if #available(iOS 10.0, *) {
            let center = UNUserNotificationCenter.current()
            center.requestAuthorization(options: [.sound, .alert], completionHandler: { (granted, error) in
                if let err = error {
                    self.write(
                        level: .error,
                        message: { "UNUserNotificationCenter requestAuthorization failed! \(err)" },
                        parameters: LogParameters(function: #function, file: #file, line: #line))
                    return
                }
            })
        }
    }
    
    func locationManager(_ manager: CLLocationManager, didChangeAuthorization status: CLAuthorizationStatus) {
        if status == .authorizedAlways {
            mobileAccess.backgroundScanningMode = .extended
            // we'll need to restart scanning
            mobileAccess.setScanning(enabled: false)
            mobileAccess.setScanning(enabled: true)
        }
    }
    
    static func mobileAccessLocalizationConfig() -> MobileAccessLocalization {
        return MobileAccessLocalization(
            notificationDetails: { reader in (title: "", body: "Open App for \(reader.name)") },
            registrationDetails: { siteName in "Register for \"\(siteName)\"" },
            authenticationDetails: { readerName in "Authenticate for \"\(readerName)\"" })
    }
}

