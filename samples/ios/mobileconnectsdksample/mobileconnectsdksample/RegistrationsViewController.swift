//
// Copyright Gallagher Group Ltd 2024 All Rights Reserved
//
import UIKit
import GallagherMobileAccess

private let log = Log.createForClass(AppDelegate.self)

class RegistrationsViewController : UITableViewController, WalletUpdateDelegate {
    
    let mobileAccess = MobileAccessProvider.instance
    
    var credentials: [MobileCredential] = []
    var wallets = [Wallet]()
    
    private var _observer: NSObjectProtocol?
    
    override func viewDidLoad() {
        super.viewDidLoad()
        
        _observer = NotificationCenter.default.addObserver(forName: UIApplication.willEnterForegroundNotification, object: nil, queue: nil) { [weak self] (notification) in
            guard let this = self else { return }
            this.viewDidAppear(false)
        }
        
        if #available(iOS 16, *) {
            mobileAccess.addWalletUpdateDelegate(self)
            // Fetch any registered mobile credential updates from the Gallagher Cloud
            mobileAccess.syncCredentialItemUpdates()
        }
    }
    
    deinit {
        if let o = _observer {
            NotificationCenter.default.removeObserver(o)
        }
        
        if #available(iOS 16, *) {
            mobileAccess.removeWalletUpdateDelegate(self)
        }
    }
    
    @IBAction func sendLogs(_ sender: Any) {
        do {
            try exportLogFiles()
        } catch let e {
            log.error("Couldn't get logs with error \(e)")
            return
        }
    }
    
    func getLogFileUrls() throws -> [URL]? {
        // Attach all the log files in the 'Documents' folder ..
        guard let documentsDirectory = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first else {
            return nil
        }
        
        let files: [URL] = try FileManager.default.contentsOfDirectory(at: documentsDirectory, includingPropertiesForKeys: nil, options: [])
        
        // Filter out the files that don't end with ‘.log’ – users might transfer other type of files to the 'documents' directory via iTunes
        return files.filter { $0.pathExtension == "log" }
    }
        
    func exportLogFiles() throws {
        guard let files = try getLogFileUrls() else {
            return
        }

        let activityItems: [Any] = ["GallagherMobileAccess Sample App log files"] + files
        
        let activityViewController = UIActivityViewController(activityItems: activityItems, applicationActivities: nil)
            
        self.present(activityViewController, animated: true)
    }
    
    override func viewWillAppear(_ animated: Bool) {
        super.viewWillAppear(animated)
        
        // Check with the cloud each time our view is made visible.
        // This may not be appropriate if your view hides and shows rapidly
        mobileAccess.syncCredentialItemUpdates()
    }
    
    // reload on didAppear so if they exit the app, change something about the credential status, then come back in, it will refresh
    override func viewDidAppear(_ animated: Bool) {
        credentials = mobileAccess.mobileCredentials
        tableView.reloadData()
        
        // Convenient way to register for any sdk feature states/errors instead of implementing SdkFeatureStateDelegate directly on the view controller class
        // here we are only interested to know about the Wallet feature state/errors
        mobileAccess.addSdkFeatureStateDelegate { [weak self] featureStates in
            guard let this = self else { return }

            if featureStates.contains(.errorCloudConnectionFailed) {
                this.mobileAccess.syncCredentialItemUpdates() // try connecting again
            } else if featureStates.contains(.errorMobileWalletNotSupported) {
                toast(".wallet feature is not supported for this device, the feature is only available for 'iPhone' devices supporting iOS16 and later")
                this.tableView.reloadData()
            } else if !featureStates.isEmpty {
                log.info("Encountered other feature sdk errors this view isn't interested about")
            }

        } onFeatureError: { error in
            // This function will return any errors encountered by any of the features enabled for the SDK, you can choose to handle only what applies to the view
            if let walletError = error as? WalletUpdateError {
                toast("Wallet update error\n\(walletError)")
            }
        }
    }
    
    func onInvitationDetails(succeeded: Bool, invitationCode: String, serverHost: String) {
        if succeeded {
            // *********************************************************************************
            // When doing manual registration we build the URI ourselves.
            // Normally we'd expect the full URI to be passed to us e.g. from an email hyperlink,
            // or from some other custom code (perhaps you pass the URI through your own web-service
            // *********************************************************************************
            guard let invitationUrl = mobileAccess.resolveInvitationUrl(serverHost, invitation: invitationCode) else { return }
            
            // *********************************************************************************
            // Ask the Mobile Connect SDK to register our credential
            // *********************************************************************************
            mobileAccess.registerCredential(
                url: invitationUrl,
                onRegistrationCompleted: { credential, error in
                    if let error = error {
                        toast("Registration Error \(error)")
                    }
                    else {
                        toast("Registered!")
                        self.credentials = self.mobileAccess.mobileCredentials
                        self.tableView.reloadData()
                    }
                },
                onAuthenticationTypeSelectionRequested: { selector in
                    let alert = UIAlertController(title: "Second Factor", message: "Please select second factor authentication type", preferredStyle: .alert)
                    alert.addAction(UIAlertAction(title: "Fingerprint/FaceID", style: .default, handler: { _ in selector(true, .fingerprintOrFaceId) }))
                    alert.addAction(UIAlertAction(title: "Passcode", style: .default, handler: { _ in selector(true, .pin) }))
                    alert.addAction(UIAlertAction(title: "Cancel", style: .cancel, handler: { _ in selector(false, nil) }))
                    self.present(alert, animated: true, completion: nil)
                })
        }
    }
    
    func onMobileCredentialClicked(credential: MobileCredential) {
        let alert = UIAlertController(title: "Are you sure you want to delete the credential for \(credential.facilityName)", message: nil, preferredStyle: .alert)
        alert.addAction(UIAlertAction(title: "Yes", style: .default, handler: { _ in
            // *********************************************************************************
            // Ask the Mobile Connect SDK to delete our credential
            // *********************************************************************************
            self.mobileAccess.deleteMobileCredential(credential, deleteOption: .default) { (credential, error) in
                if let error = error {
                    toast("Error deleting credential \(error)")
                }
                else {
                    toast("Deleted!")
                    self.credentials = self.mobileAccess.mobileCredentials
                    self.tableView.reloadData()
                }
            }
        }))
        alert.addAction(UIAlertAction(title: "Cancel", style: .cancel))
        present(alert, animated: true, completion: nil)
    }
    
    // MARK: - Navigation
    
    override func prepare(for segue: UIStoryboardSegue, sender: Any?) {
        if let registerNc = segue.destination as? UINavigationController,
            let registerVc = registerNc.topViewController as? RegisterMobileCredentialsDetailsViewController {
            registerVc.delegate = self
        }
    }
    
    // MARK: - TableView
    
    override func numberOfSections(in tableView: UITableView) -> Int {
        return 1
    }
    
    override func tableView(_ tableView: UITableView, numberOfRowsInSection section: Int) -> Int {
        return credentials.count
    }
    
    override func tableView(_ tableView: UITableView, cellForRowAt indexPath: IndexPath) -> UITableViewCell {
        let credential = credentials[indexPath.row]
        
        guard let cell = tableView.dequeueReusableCell(withIdentifier: "credentialCell", for: indexPath) as? CredentialCell else {
            fatalError("Incorrect Cell Type; Broken storyboard?")
        }
        cell.display(credential: credential)
        
        let wallet = wallets.first(where: { $0.credentialId == credential.id})
        cell.displayWallet(wallet: wallet, onAddToWalletClicked: addWallet)

        return cell
    }
        
    override func tableView(_ tableView: UITableView, didSelectRowAt indexPath: IndexPath) {
        let credential = credentials[indexPath.row]
        onMobileCredentialClicked(credential: credential)
        tableView.deselectRow(at: indexPath, animated: true)
    }
    
    // MARK: - WalletUpdateDelegate
    
    // *********************************************************************************
    // WalletUpdateDelegate:
    // The MobileConnect SDK will only return wallet updates if the site has fully configured the Apple Access Badge solution
    // and the registered mobile credential has received a wallet update
    // Adding/removing the pass from the wallet app will also trigger an update that will come through this delegate
    // *********************************************************************************
    func onWalletsUpdated(_ addedOrUpdatedWallets: [Wallet], _ removedWallets: [Wallet]) {
        for wallet in addedOrUpdatedWallets {
            if let walletIndex = wallets.firstIndex(where: { $0.credentialId == wallet.credentialId }) {
                wallets[walletIndex] = wallet
            } else {
                wallets.append(wallet)
            }
            
            updateCredentialCellWithWallet(wallet)
        }
        
        for wallet in removedWallets {
            wallets.removeAll(where: {$0.credentialId == wallet.credentialId })
            
            updateCredentialCellWithWallet(wallet)
        }
    }
    
    // MARK: - wallet feature helper functions
    
    private func addWallet(wallet: Wallet?) {
        guard #available(iOS 16, *) else {
            log.info("Wallet feature only supported for iOS 16 and later")
            return
        }

        guard let wallet else {
            log.info("Nil Wallet cannot be added")
            return
        }

        guard let templatePassImage = UIImage.init(named: "WalletPassTemplate") else {
            log.error("Failed to load the wallet pass template image")
            return
        }
        
        // You can choose to implement 'WalletProvisioningDelegate' on a concrete class or use the extension function below provide a block handler for `onWalletMigrationDetected` and `onProvisioningCompleted`
        mobileAccess.startProvisioningWallet(
            wallet,
            passThumbnailImage: templatePassImage,
            passDescription: "Adding Sample App Apple Pass",
            presentingViewController: self,
            onWalletMigrationDetected: { handler in
                // Getting called here means that the SDK detected a migration of the pass to a new device,
                // calling the handler with true will allow the SDK to continue the provisioning which will delete the pass from the old device and provision it to the new device
                // calling the handler with false will tell the SDK to abort and stop the provisioning process
                // Note: you can choose to prompt the user to confirm the migration before continuing this will depend on your business requirement.
                handler(true)
            },
            onWalletProvisioningCompleted: { [weak self] wallet, succeeded, error in
                guard let this = self else { return }
                
                if succeeded {
                    this.updateCredentialCellWithWallet(wallet)
                } else if let e = error {
                    if case .userCancelledProvisioning = e {
                        toast("Provisioning wallet cancelled")
                    } else if case .prepareProvisioningError(let message, code: let errorCode) = e {
                        toast("Provisioning wallet failed")
                        log.error("prepareProvisioningError for credential \(wallet.credentialId) failed with message \(message) and errorCode \(errorCode).")
                    } else {
                        toast("Provisioning wallet failed, try again later.")
                        log.error("provisioning failed for credential \(wallet.credentialId) failed with error \(e).")
                    }
                }
            })
    }
    
    private func updateCredentialCellWithWallet(_ wallet: Wallet) {
        DispatchQueue.main.async {
            // update the related credential cell, reloading the row will redraw the cell and decide accordingly to show/hide the wallet view
            if let mobileCredentialIndexToUpdate = self.credentials.firstIndex(where: { $0.id == wallet.credentialId }) {
                self.tableView.reloadRows(at: [IndexPath(row: mobileCredentialIndexToUpdate, section: 0)], with: .automatic)
            }
        }
    }
}

class CredentialCell : UITableViewCell {
    @IBOutlet private weak var _facilityNameLabel: UILabel!
    @IBOutlet private weak var _registeredDateLabel: UILabel!
    @IBOutlet private weak var _secondFactorLabel: UILabel!
    @IBOutlet private weak var _statusLabel: UILabel!
    
    func display(credential: MobileCredential) {
        _facilityNameLabel.text = credential.facilityName
        _registeredDateLabel.text = credentialDateToString(credential.registeredDate)
        _secondFactorLabel.text = credential2fToString(credential.secondFactorType)
        _statusLabel.text = credentialStatusToString(credential.status)
    }
    
    // MARK: - wallet feature helpers
    
    // UI controls and variables used for sample use of the wallet feature
    @IBOutlet private weak var _walletView: UIStackView!
    @IBOutlet private weak var _viewInWalletButton: UIButton!
    // _addToWalletButton UIButton is "PKAddPassButton" with custom type to ensure the design is compliant.
    // Always follow the official Add to Apple Wallet Guidelines (https://developer.apple.com/wallet/add-to-apple-wallet-guidelines/) for displaying the Add to Apple Wallet button in your app.
    @IBOutlet private weak var _addToWalletButton: UIButton!
    
    private var _wallet: Wallet?
    private var _onAddToWalletClicked: ((_ wallet: Wallet) -> Void)!
    
    func displayWallet(wallet: Wallet?, onAddToWalletClicked: @escaping(_ wallet: Wallet) -> Void) {
        _wallet = wallet
        _onAddToWalletClicked = onAddToWalletClicked
        
        if _wallet == nil { // there is no wallet to display, then just hide the view and the buttons
            _walletView.isHidden = true
            _addToWalletButton.isHidden = true
            _viewInWalletButton.isHidden = true
            return
        }
        
        _walletView.isHidden = false
        // Show the "Add to Wallet" button to indicate to the user that a pass can be provisioned to the phone and/or the paired watch
        if let wallet = _wallet, wallet.shouldShowAddToWalletButton() {
            _addToWalletButton.isHidden = false
            _viewInWalletButton.isHidden = true
        } else {
            _addToWalletButton.isHidden = true
            _viewInWalletButton.isHidden = false
        }
    }
    
    @IBAction func addToWalletClicked(_ sender: Any) {
        guard let wallet = _wallet else { return }
        _onAddToWalletClicked(wallet)
    }
    
    @IBAction func viewInWalletClicked(_ sender: Any) {
        guard let walletUrl = _wallet?.passUrl else { return }
        UIApplication.shared.open(walletUrl)
    }
    
    // MARK: - private helper functions
    
    private func credentialDateToString(_ date: Date) -> String {
        if date.timeIntervalSince1970 == 0 {
            return "Unknown"
        } else {
            let dateFormatter = DateFormatter()
            dateFormatter.dateStyle = .short
            dateFormatter.timeStyle = .none
            return dateFormatter.string(from: date)
        }
    }
    
    private func credential2fToString(_ secondFactorType: SecondFactorVerificationType?) -> String {
        switch secondFactorType {
        case .some(.passcode): return "Passcode"
        case .some(.biometric): return "TouchID or FaceID"
        case .none: return "Unknown"
        @unknown default: fatalError("Unsupported future type")
        }
    }
    
    private func credentialKeyStatusToString(_ status: MobileCredentialKeyStatus) -> String {
        switch status {
        case .good: return "Good"
        case .notRegistered: return "Not Registered"
        case .unknown: return "Unknown"
        case .bad(let err): return "Bad: \(err?.localizedDescription ?? "")"
        @unknown default: fatalError("Unsupported future type")
        }
    }
        
    private func credentialStatusToString(_ status: MobileCredentialStatus) -> String {
        return "1f: \(credentialKeyStatusToString(status.singleFactor)), 2f: \(credentialKeyStatusToString(status.secondFactor))"
    }
}

class RegisterMobileCredentialsDetailsViewController : UIViewController {
    weak var delegate: RegistrationsViewController?
    
    @IBOutlet weak var invitationCodeTextField: UITextField!
    @IBOutlet weak var cloudUrlTextFIeld: UITextField!
    
    @IBAction func registerButtonClicked(_ sender: Any) {
        delegate?.onInvitationDetails(
            succeeded: true,
            invitationCode: invitationCodeTextField.text ?? "",
            serverHost: cloudUrlTextFIeld.text ?? "")
        dismiss(animated: true, completion: nil)
    }
    
    @IBAction func cancelButtonClicked(_ sender: Any) {
        delegate?.onInvitationDetails(
            succeeded: false,
            invitationCode: "",
            serverHost: "")
        dismiss(animated: true, completion: nil)
    }
    
    override func viewDidLoad() {
        super.viewDidLoad()
        
        // Convenience for manual registration - if the clipboard looks like it has an invitation code, paste it in
        if let clipText = UIPasteboard.general.string {
            let regex = try! NSRegularExpression(pattern: "^\\s?\\w{4}-\\w{4}-\\w{4}-\\w{4}\\s?$")
            if regex.firstMatch(in: clipText, options: [], range: NSRange(clipText.startIndex..., in: clipText)) != nil {
                invitationCodeTextField.text = clipText.trimmingCharacters(in: .whitespaces)
            }
        }
    }
}
