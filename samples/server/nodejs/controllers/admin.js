const express = require('express');
const router = express.Router();

// Load the admin portal, for creating cardholders etc
module.exports = function (usingDatabase, gglApi, commandCentreConfig) {

  router.get('/', async (req, res) => {
    usingDatabase(async db => {
      const students = await db.allAsync("SELECT id, username, studentId from students");

      res.render('admin/index', { students: students, error: req.query.error });
    });
  });

  router.post('/deleteStudent', async (req, res) => { // should be DELETE but browsers only allow forms with GET and POST
    usingDatabase(async db => {

      // try delete them from command centre if we have their record linked
      const student = await db.getAsync('SELECT commandCentreHref FROM students WHERE id = ?;', req.query.id);
      if (!student || !student.commandCentreHref) {
        // we could do a cardholder search using their StudentID here if we wanted to
        res.redirect("/admin?error=This student is not linked to Command Centre");
        return;
      }

      const result = await gglApi.deleteAsync(student.commandCentreHref, null);
      if (result.statusCode != 204) {
          // we failed, try read the body
          const message = ("body" in result && "message" in result.body) ? result.body.message : result.statusMessage;
          res.redirect(`/admin?&error=${message}`);
          return;
      }

      // delete from our local database
      db.runAsync(`DELETE FROM students WHERE id = ?;`, req.query.id);
      res.redirect("/admin?message=Deleted Student");
    });
  });

  router.post('/createStudent', async (req, res) => {
    usingDatabase(async db => {

      // create cardholder in Command Centre with the given student ID (if the PDF enforces uniqueness this can fail)
      const result = await gglApi.postAsync(commandCentreConfig.cardholderSearchHref, {
        "firstName": req.body.firstName,
        "lastName": req.body.lastName,
        "authorised": true, // if they are not authorised, they can't gain access
        "division": { "href": commandCentreConfig.rootDivisionHref },
        "accessGroups": [
          { "accessGroup": { "href": commandCentreConfig.studentAccessGroupHref } }
        ],

        // Cardholder PDF values are provided in the form:  @<pdf-name>: value
        [`@${commandCentreConfig.keyPersonalDataFieldName}`]: req.body.studentId,

        // Mobile Credentials can be issued at cardholder creation by providing the same details used for the requestMobileCredential call as follows:
        // "cards": [
        //     { "type": { "href": commandCentreConfig.mobileCredentialTypeHref } }
        // ]
      })

      // if this was successful then update the matching student in our students table
      if (result.statusCode == 201) {
        const createdCardholderHref = result.headers.location; // the response from the POST gives us the HREF of the new cardholder in the Location header

        // try create a new student in our system's own database
        await db.runAsync("INSERT INTO students SELECT null, ?, ?, ?, ?, ?;", req.body.username, req.body.studentId, req.body.password, createdCardholderHref, false);
      res.redirect(`/admin?message=Created Cardholder`);
      }
      else {
        const message = result.body.message;
        const error = message ? message : 'Failed to create cardholder';

        res.redirect(`/admin?&error=${encodeURIComponent(error)}`);
      }
    });
  });

  return router;
}