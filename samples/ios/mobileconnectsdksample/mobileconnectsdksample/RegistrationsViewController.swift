//
// Copyright Gallagher Group Ltd 2018 All Rights Reserved
//
import UIKit
import GallagherMobileAccess

class RegistrationsViewController : UITableViewController {
    
    let mobileAccess = MobileAccessProvider.instance
    
    var credentials: [MobileCredential] = []
    
    private var _observer: NSObjectProtocol?
    
    override func viewDidLoad() {
        super.viewDidLoad()
        
        _observer = NotificationCenter.default.addObserver(forName: UIApplication.willEnterForegroundNotification, object: nil, queue: nil) { [weak self] (notification) in
            guard let this = self else { return }
            this.viewDidAppear(false)
        }
    }
    
    deinit {
        if let o = _observer {
            NotificationCenter.default.removeObserver(o)
        }
    }
        
    // reload on didAppear so if they exit the app, change something about the credential status, then come back in, it will refresh
    override func viewDidAppear(_ animated: Bool) {
        credentials = mobileAccess.mobileCredentials
        tableView.reloadData()
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
            self.mobileAccess.deleteMobileCredential(credential) { (credential, error) in
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
        return cell
    }
    
    override func tableView(_ tableView: UITableView, didSelectRowAt indexPath: IndexPath) {
        let credential = credentials[indexPath.row]
        onMobileCredentialClicked(credential: credential)
        tableView.deselectRow(at: indexPath, animated: true)
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
        case .bad(let err): return "Bad: \(err.localizedDescription)"
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
