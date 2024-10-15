## Disclaimer

This project contains sample code that is provided **for educational purposes only**. The code in this repository is **not actively maintained** and should **not be used in production environments**.

- The sample code does not receive security updates or bug fixes.
- There is no guarantee of stability, performance, or correctness.
- **Do not use this code in production systems** without proper security and performance reviews.

## Pre-requisites

You should read the document **Mobile Connect SDK Integration Guide TIP** in conjunction with this sample application.

## Command Centre Pre-requisites

This sample application assumes the Command Centre REST api is running.  
NOTE: Please check "host" in config.json is correct for your server.

You will need to configure the following in Command Centre:

- An operator account with the following privileges: 
- (these are the minimum required privileges for the functions used in the app)
  -> Create and Edit Cardholders
  -> Delete Cardholders
  -> View Personal Data Definitions
  -> View Site (to view Divisions and Access Groups)
  -> Modify Access Control (to assign Cardholders to Access Groups)

- A REST client set to use that operator account.  
  *NOTE:* You will need to put the sha1 thumbprint of the sample client certificate into Command Centre. See config.json
  *NOTE:* You will need to put the generated api key in config.json

- A personal data field called "Student ID" - Numeric format.  
  *NOTE:* If you wish to use a different PDF, put it's name in config.json

- A cardholder who has the value "12345" for their "Student ID" personal data field.

- A Mobile Credential card type called "Mobile Credential". Command Centre has this by default already.  
  *NOTE:* If you wish to use a different card type, put it's name in config.json

## Software Pre-requisites:

You will need the following installed:

* Node.js     (developed using Node v10.15.1 on Windows 10)
* npm         (developed using npm 6.4.1)

*NOTE:* npm is bundled with nodejs so you don't need to install it separately.

Run the following commands:

    npm install --save
    node server.js

*NOTE:* npm install is only needed on first-run.

Then, point your browser at http://localhost:3000

First go the Admin area, and use it to create a new cardholder.
Then, you can go to the Student area and log in as that person, and request a new mobile credential