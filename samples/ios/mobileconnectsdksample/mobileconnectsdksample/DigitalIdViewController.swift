//
// Copyright Gallagher Group Ltd 2022 All Rights Reserved
//
import UIKit
import GallagherMobileAccess

class DigitalIdViewController : UITableViewController, SdkFeatureStateDelegate, DigitalIdDelegate {
    
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
    
    private var _digitalIds: [DigitalId] = []
    
    override func viewDidLoad() {
        super.viewDidLoad()
        
        mobileAccess.addSdkFeatureStateDelegate(self)
        mobileAccess.addDigitalIdDelegate(self)
    }
    
    deinit {
        mobileAccess.removeDigitalIdDelegate(self)
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
    // Manage the SDK's connection to the cloud for fetching Digital ID's
    // *********************************************************************************
    
    // This is how the SDK tells us about the state of the Digital ID cloud connection
    // Note: this is stateful. The cloud connection is either valid, or it's not. If we get 10 errors
    // we may not get 10 state changes here, we may simply get an .errorCloudConnectionFailed once.
    // to make this go away, re-establish the network connection to the cloud, and then call mobileAccess.syncCredentialItemUpdates()
    func onFeatureStatesChanged(featureStates: [SdkFeatureState]) {
        _hasCloudConnectionError = featureStates.contains(.errorCloudConnectionFailed)
    }
    
    // This is how the SDK tells us about any detailed errors that occur.
    // Note: this is for error events themselves. If we get 10 errors, this method will be called 10 times
    func onFeatureError(error: Error) {
        if let digitalIdError = error as? DigitalIdError {
            toast("Digital Id update error\n\(digitalIdError.localizedDescription)")
        }
    }
    
    // *********************************************************************************
    // Manage the Digital ID's themselves
    // *********************************************************************************
    
    func onDigitalIdUpdated(_ addedOrUpdatedDigitalIds: [DigitalId], _ removedDigitalIds: [DigitalId], lastUpdateTime: Date?) {
        // update our list of DigitalIds
        for digitalId in addedOrUpdatedDigitalIds {
            let updateIndex = _digitalIds.firstIndex { $0.id == digitalId.id }
            
            if let idx = updateIndex {
                _digitalIds[idx] = digitalId
                tableView.reloadRows(at: [IndexPath(row: idx, section: 1)], with: .automatic)
            } else {
                _digitalIds.append(digitalId)
                tableView.insertRows(at: [IndexPath(row: _digitalIds.count-1, section: 1)], with: .automatic)
            }
        }
        
        for removedDigitalId in removedDigitalIds {
            if let removeIdx = _digitalIds.firstIndex(where: { $0.id == removedDigitalId.id }) {
                _digitalIds.remove(at: removeIdx)
                tableView.deleteRows(at: [IndexPath(row: removeIdx, section: 1)], with: .automatic)
            }
        }
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
        case 1: return _digitalIds.count
        default: fatalError("tableView should not have more than two sections")
        }
    }
    
    override func tableView(_ tableView: UITableView, cellForRowAt indexPath: IndexPath) -> UITableViewCell {
        switch indexPath.section {
        case 0:
            guard let cell = tableView.dequeueReusableCell(withIdentifier: "DigitalIdWarningTableViewCell") as? DigitalIdWarningTableViewCell else {
                fatalError("Could not dequeue cell of type DigitalIdWarningTableViewCell")
            }
            cell.parent = self
            return cell
        case 1:
            guard let cell = tableView.dequeueReusableCell(withIdentifier: "DigitalIdTableViewCell") as? DigitalIdTableViewCell else {
                fatalError("Could not dequeue cell of type DigitalIdTableViewCell")
            }
            cell.setDigitalId(_digitalIds[indexPath.row])
            return cell
        default: fatalError("tableView should not have more than two sections")
        }
    }
}

class DigitalIdTableViewCell : UITableViewCell {
    @IBOutlet private weak var _imageView: UIImageView!
    
    func setDigitalId(_ digitalId: DigitalId) {
        _imageView.image = UIImage(data: digitalId.frontSide)
        // note: the sample app does not include code for rendering the rear side
        // of a digital ID.
    }
}

class DigitalIdWarningTableViewCell : UITableViewCell {
    weak var parent: DigitalIdViewController? = nil
    
    @IBAction func retryPressed(_ sender: Any) {
        parent?.onRetryCloudConnection()
    }
}
