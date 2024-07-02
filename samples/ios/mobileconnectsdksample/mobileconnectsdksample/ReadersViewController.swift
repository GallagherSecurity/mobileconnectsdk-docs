//
// Copyright Gallagher Group Ltd 2024 All Rights Reserved
//
import Foundation
import UIKit
import GallagherMobileAccess

private enum ReaderVisualState : String {
    case connecting, granted, denied, requested
}

// "ViewModel" to render our reader information along with connection state
private class ReaderWithVisualState {
    var reader: ReaderAttributes
    var visualState: ReaderVisualState?
    
    init(reader: ReaderAttributes, visualState: ReaderVisualState?) {
        self.reader = reader
        self.visualState = visualState
    }
}

class ReadersViewController : UITableViewController, SdkStateDelegate, ReaderUpdateDelegate, AccessDelegate {

    private var messages: [String] = []
    fileprivate var readers: [ReaderWithVisualState] = []
    
    private func setReaderVisualState(_ reader: Reader, _ visualState: ReaderVisualState?) {
        if let idx = readers.firstIndex(where: { $0.reader.id == reader.id }) {
            readers[idx].visualState = visualState
            tableView.reloadRows(at: [IndexPath(row: idx, section: 1)], with: .none)
        }
    }
    
    // *********************************************************************************
    // Get a reference to the MobileAccess shared instance
    // *********************************************************************************
    let mobileAccess = MobileAccessProvider.instance
    
    override func viewDidLoad() {
        super.viewDidLoad()
        
        // *********************************************************************************
        // Ask the SDK to tell us about it's operational state so we can show warning messages if needed
        // *********************************************************************************
        mobileAccess.addSdkStateDelegate(self)
        
        // *********************************************************************************
        // Ask the SDK to tell us about readers it discovers
        // *********************************************************************************
        mobileAccess.addReaderUpdateDelegate(self)
        
        // *********************************************************************************
        // Ask the SDK to tell us about automatic access so we can show UI if needed
        // *********************************************************************************
        mobileAccess.addAutomaticAccessDelegate(self)
    }
    
    deinit {
        mobileAccess.removeAutomaticAccessDelegate(self)
        mobileAccess.removeReaderUpdateDelegate(self)
        mobileAccess.removeSdkStateDelegate(self)
    }

    // MARK: - State management
    
    // *********************************************************************************
    // SdkStateDelegate:
    // The MobileConnect SDK will publish the list of problems/warnings via this callback.
    // so we can use it to show warning messages and things like that
    // *********************************************************************************
    func onStateChange(isScanning:Bool, states: [MobileAccessState]) {
        var messages = [String]()
        
        if isScanning {
            messages.append("SDK is scanning for readers...")
        } else {
            messages.append("SDK is not scanning for readers!")
        }
        
        for state in states {
            switch state {
            case .errorNoCredentials:
                messages.append("Please register a credential")
            case .errorNoPasscodeSet:
                messages.append("Please set a passcode on your device")
            case .errorDeviceNotSupported:
                messages.append("Your device is not supported")
            case .bleWarningExtendedBackgroundScanningRequiresLocationServiceEnabled:
                messages.append("Location services are turned off. Please turn it on to enable background scanning")
            case .bleWarningExtendedBackgroundScanningRequiresLocationAlwaysPermission:
                messages.append("Please grant ALWAYS permission to the location services to enable background scanning")
            case .errorNoBleFeature:
                messages.append("Bluetooth is not supported on this device")
            case .bleErrorUnauthorized:
                messages.append("Please grant permission for this application to use Bluetooth.")
            case .bleErrorDisabled:
                messages.append("Bluetooth is turned off. Please turn it on")
            case .credentialRequiresBiometricsEnrolment:
                messages.append("One of your mobile credentials requires a fingerprint or faceID, which is not set up on this device. Please set up a fingerprint or face ID in your phone system settings.")
            default:
                break // ignore unsupported states such as those that can only occur on android
            }
        }
        
        self.messages = messages
        tableView.reloadSections(IndexSet(0...0), with: .automatic)
    }
    
    // MARK: - Reader updates
    
    // Triggered when nearby Gallagher or Aperio BLE readers state changes
    func onReaderUpdated(_ reader: ReaderAttributes, updateType: ReaderUpdateType) {
        if updateType == .attributesChanged {
            if let idx = readers.firstIndex(where: { $0.reader.id == reader.id }) {
                readers[idx].reader = reader
                
                // don't reload the row, reader updates happen all the time and it interferes with scrolling
                // If we do need to modify some visual aspect of the row, the right way to do it is
                // get a reference to the TableViewCell and modify it's appearance directly
                
            } else { // a new reader, put it at the top
                readers.insert(ReaderWithVisualState(reader: reader, visualState: nil), at: 0)
                tableView.insertRows(at: [IndexPath(row: 0, section: 1)], with: .automatic)
            }
        } else if updateType == .readerUnavailable {
            if let idx = readers.firstIndex(where: { $0.reader.id == reader.id }) {
                readers.remove(at: idx)
                tableView.deleteRows(at: [IndexPath(row: idx, section: 1)], with: .automatic)
            }
        }
    }
    
    // MARK: - Access
    
    private func onReaderClicked(reader: Reader) { // for manual connect
        mobileAccess.requestAccess(reader: reader, delegate: self)
    }
    
    // *********************************************************************************
    // AccessDelegate (which is also the AccessDelegate for manual connect requests):
    // The MobileConnect SDK is telling us access is in progress for the given reader
    // so we may update the appropriate UI for that reader (e.g. show an animation)
    // *********************************************************************************
    func onAccessStarted(reader: Reader) {
        setReaderVisualState(reader, .connecting)
    }
    
    // *********************************************************************************
    // AccessDelegate:
    // The MobileConnect SDK is telling us access successfully completed for the given reader with a non-nil result passed back
    // The MobileConnect SDK is telling us access failed for the given reader with a non-nil error passed back
    // Note: Access result includes those triggered by Mobile Credentials and Aperio Credentials
    // *********************************************************************************
    func onAccessCompleted(reader: Reader, result: AccessResult?, error: ReaderConnectionError?) {
        // 'error' only occurs if there's some sort of lower-level error (e.g. bluetooth disconnect)
        // in the normal case error will be nil, and you should check accessResult.isAccessGranted()
        // and accessResult.isAccessDenied(). There are cases when it will be neither if the door
        // does not support feedback. accessResult.getAccessDecision() is the actual specific result
        // behind the scenes
        if let accessResult = result {
            if accessResult.isAccessGranted() {
                setReaderVisualState(reader, .granted)
            } else if accessResult.isAccessDenied() {
                setReaderVisualState(reader, .denied)
            } else {
                setReaderVisualState(reader, .requested)
            }
        } else {
            setReaderVisualState(reader, .denied)
        }
        
        // The SDK doesn't give us any more callbacks after accessComplete
        // so set a timer to clear the visual state after a small delay.
        DispatchQueue.main.asyncAfter(deadline: .now() + 1) {
            self.setReaderVisualState(reader, nil)
        }
    }
   
    // MARK: - Actions
    
    private func onReaderActions(reader: Reader) { // invoked from a swipe action on the table row
        mobileAccess.enumerateReaderActions(reader: reader) { (reader, actions, error) in
            if let error = error { // The mobile connect SDK is telling us an enumerateReaderActions encountered an error such as a BLE disconnect
                self.tableView.setEditing(false, animated: true)
                toast("enumerateReaderActions failed: \(error)")
            }
            else if let actions = actions { // The mobile connect SDK is telling us it successfully got the list of actions back from an enumerateReaderActions request
                if actions.count == 0 {
                    toast("No action available")
                    return
                }
                
                let alert = UIAlertController(title: nil, message: nil, preferredStyle: .actionSheet)
                for action in actions {
                    alert.addAction(UIAlertAction(title: action.name, style: .default, handler: { _ in
                        self.doAction(reader: reader, action: action)
                        alert.dismiss(animated: true, completion: nil)
                    }))
                }
                
                alert.addAction(UIAlertAction(title: "Cancel", style: .cancel, handler: { _ in
                    self.tableView.setEditing(false, animated: true)
                    alert.dismiss(animated: true, completion: nil)
                }))
                
                self.present(alert, animated: true, completion: nil)
            }
        }
    }

    private func doAction(reader: Reader, action: ReaderAction) {
        mobileAccess.requestReaderAction(reader: reader, action: action) { (reader, action, error) in
            if let error = error { //The mobile connect SDK is telling us an action request encountered an error such as a BLE disconnect
                toast("action error: \(error)")
                self.tableView.setEditing(false, animated: true)
            }
            else { // The mobile connect SDK is telling us an action request completed
                self.tableView.setEditing(false, animated: true)
                toast("\(action.name) succeeded")
            }
        }
    }
    
    // MARK: - TableView
    
    override func numberOfSections(in tableView: UITableView) -> Int {
        return 2
    }
    
    override func tableView(_ tableView: UITableView, numberOfRowsInSection section: Int) -> Int {
        return section == 0 ? messages.count : readers.count
    }
    
    override func tableView(_ tableView: UITableView, cellForRowAt indexPath: IndexPath) -> UITableViewCell {
        let cell = tableView.dequeueReusableCell(withIdentifier: "readerCell", for: indexPath)
        
        switch indexPath.section {
        case 0:
            let message = messages[indexPath.row]
            cell.textLabel?.text = message
        default:
            let rws = readers[indexPath.row]
            let reader = rws.reader
            
            let visualStateString = rws.visualState?.rawValue ?? ""
            cell.textLabel?.text = "\(visualStateString): \t\(reader.name)"
        }
        return cell
    }
    
    override func tableView(_ tableView: UITableView, didSelectRowAt indexPath: IndexPath) {
        if indexPath.section == 1 {
            let rws = readers[indexPath.row]
            onReaderClicked(reader: rws.reader)
        }
        tableView.deselectRow(at: indexPath, animated: true)
    }
    
    override func tableView(_ tableView: UITableView, editActionsForRowAt indexPath: IndexPath) -> [UITableViewRowAction]? {
        if indexPath.section == 1 {
            return [UITableViewRowAction(style: .default, title: "Actions", handler: { [weak self](action, ixpath) in
                guard let this = self else { return }
                
                let rws = this.readers[ixpath.row]
                this.onReaderActions(reader: rws.reader)
            })]
        }
        return nil
    }
    
}
