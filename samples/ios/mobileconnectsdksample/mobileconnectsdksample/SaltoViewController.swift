//
// Copyright Gallagher Group Ltd 2020 All Rights Reserved
//
import UIKit
import GallagherMobileAccess

class SaltoViewController : UITableViewController, SdkFeatureStateDelegate, SaltoUpdateDelegate {
    
    let mobileAccess = MobileAccessProvider.instance
    
    private var _hasCloudConnectionError: Bool = false {
        didSet {
            if _hasCloudConnectionError != oldValue {
                if _hasCloudConnectionError { // adding the error in the top section
                    tableView.insertRows(at: [IndexPath(row: 0, section: 0)], with: .automatic)
                } else { // removing the error
                    tableView.deleteRows(at: [IndexPath(row: 0, section: 0)], with: .automatic)
                }
            }
        }
    }
    
    private var _saltoKeys: [SaltoKeyIdentifier] = []
    
    override func viewDidLoad() {
        super.viewDidLoad()
        
        mobileAccess.addSdkFeatureStateDelegate(self)
        mobileAccess.addSaltoUpdateDelegate(self)
    }
    
    deinit {
        mobileAccess.removeSaltoUpdateDelegate(self)
        mobileAccess.removeSdkFeatureStateDelegate(self)
    }
    
    override func viewWillAppear(_ animated: Bool) {
        super.viewWillAppear(animated)
        
        // Check with the cloud each time our view is made visible.
        // This may not be appropriate if your view hides and shows rapidly
        mobileAccess.syncCredentialItemUpdates()
    }
    
    func onRetryCloudConnection() {
        mobileAccess.syncCredentialItemUpdates()
    }
    
    // *********************************************************************************
    // Manage the SDK's connection to the cloud for fetching Salto Keys
    // *********************************************************************************
    
    // This is how the SDK tells us about the state of the cloud connection
    // Note: this is stateful. The cloud connection is either valid, or it's not. If we get 10 errors
    // we may not get 10 state changes here, we may simply get an .errorCloudConnectionFailed once.
    // to make this go away, re-establish the network connection to the cloud, and then call mobileAccess.syncCredentialItemUpdates()
    func onFeatureStatesChanged(featureStates: [SdkFeatureState]) {
        _hasCloudConnectionError = featureStates.contains(.errorCloudConnectionFailed)
    }
    
    // This is how the SDK tells us about any detailed errors that occur.
    // Note: this is for error events themselves. If we get 10 errors, this method will be called 10 times
    func onFeatureError(error: Error) {
        if let saltoError = error as? SaltoError {
            toast("Salto update error\n\(saltoError.localizedDescription)")
        }
    }
    
    // *********************************************************************************
    // Manage the Salto Keys themselves
    // *********************************************************************************
    
    func onSaltoKeyUpdated(_ addedOrUpdatedSaltoKeyIdentifiers: [SaltoKeyIdentifier], _ removedSaltoKeyIdentifiers: [SaltoKeyIdentifier]) {
        // update our list of keys
        for saltoKey in addedOrUpdatedSaltoKeyIdentifiers {
            // you must check both that credentialId and saltoServerId match to find the exact key
            let updateIndex = _saltoKeys.firstIndex { $0.credentialId == saltoKey.credentialId && $0.saltoServerId == saltoKey.saltoServerId }
            
            if let idx = updateIndex {
                _saltoKeys[idx] = saltoKey
                tableView.reloadRows(at: [IndexPath(row: idx, section: 1)], with: .automatic)
            } else {
                _saltoKeys.append(saltoKey)
                tableView.insertRows(at: [IndexPath(row: _saltoKeys.count-1, section: 1)], with: .automatic)
            }
        }
        
        for removedSaltoKey in removedSaltoKeyIdentifiers {
            if let removeIdx = _saltoKeys.firstIndex(where: { $0.credentialId == removedSaltoKey.credentialId && $0.saltoServerId == removedSaltoKey.saltoServerId }) {
                _saltoKeys.remove(at: removeIdx)
                tableView.deleteRows(at: [IndexPath(row: removeIdx, section: 1)], with: .automatic)
            }
        }
    }
    
    func onSaltoKeyPressed(saltoKey: SaltoKeyIdentifier) {
        toast("Using key \(saltoKey.name ?? "?")")
        
        mobileAccess.startOpeningSaltoDoor(
            saltoKeyIdentifier: saltoKey,
            peripheralFound: {
                toast("found Salto peripheral")
            },
            saltoAccessCompleted:{ result in
                switch result {
                case .failure(let error):
                    toast("Salto access failed with error \(error.localizedDescription)")
                case .success(let accessResult):
                    toast("Salto access completed with \(accessResult.saltoAccessDecision?.description ?? "?")")
                }
            })
    }
    
    // *********************************************************************************
    // ordinary UITableView stuff
    // *********************************************************************************
    
    override func numberOfSections(in tableView: UITableView) -> Int {
        2
    }
    
    override func tableView(_ tableView: UITableView, numberOfRowsInSection section: Int) -> Int {
        switch section {
        case 0: return _hasCloudConnectionError ? 1 : 0 // hide the top row/section if there are no errors
        case 1: return _saltoKeys.count
        default: fatalError("tableView should not have more than two sections")
        }
    }
    
    override func tableView(_ tableView: UITableView, cellForRowAt indexPath: IndexPath) -> UITableViewCell {
        switch indexPath.section {
        case 0:
            guard let cell = tableView.dequeueReusableCell(withIdentifier: "SaltoWarningTableViewCell") as? SaltoWarningTableViewCell else {
                fatalError("Could not dequeue cell of type SaltoWarningTableViewCell")
            }
            cell.parent = self
            return cell
        case 1:
            guard let cell = tableView.dequeueReusableCell(withIdentifier: "SaltoKeyTableViewCell") as? SaltoKeyTableViewCell else {
                fatalError("Could not dequeue cell of type SaltoKeyTableViewCell")
            }
            cell.setSaltoKey(_saltoKeys[indexPath.row])
            return cell
        default: fatalError("tableView should not have more than two sections")
        }
    }
    
    override func tableView(_ tableView: UITableView, didSelectRowAt indexPath: IndexPath) {
        tableView.deselectRow(at: indexPath, animated: true)
        
        switch indexPath.section {
        case 1: // they selected a salto key, go and use it
            let targetKey = _saltoKeys[indexPath.row]
            onSaltoKeyPressed(saltoKey: targetKey)
        default:
            break // selecting rows in other sections doesn't do anything
        }
    }
}


class SaltoKeyTableViewCell : UITableViewCell {
    @IBOutlet private weak var _nameLabel: UILabel!
    @IBOutlet private weak var _serverIdLabel: UILabel!
    
    func setSaltoKey(_ saltoKey: SaltoKeyIdentifier) {
        _nameLabel.text = saltoKey.name
        _serverIdLabel.text = saltoKey.saltoServerId.uuidString
    }
}

class SaltoWarningTableViewCell : UITableViewCell {
    weak var parent: SaltoViewController? = nil
    
    @IBAction func retryPressed(_ sender: Any) {
        parent?.onRetryCloudConnection()
    }
}
