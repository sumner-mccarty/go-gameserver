package main

import (
        "fmt"
        "bytes"
        "github.com/hoisie/web"
		"github.com/jinzhu/gorm"
		 _ "github.com/jinzhu/gorm/dialects/mysql"
)

func main() {

	// Working DB connection and gorm helper
	//testDbAbility()
	
	// TODO: add the real API's vs the current loggers
	setupAPIs()
	
	// NOTE: this could be called via a goroutine if we wish it to not block (like if we need to init a second blocking listener)
	startWebServer()
}


func setupAPIs() {
	web.Get("/(.*)", logEverythingGET)
    web.Post("/(.*)", logEverythingPOST)
	
	// TODO: real APIs/func's 
}

func startWebServer(conn string) {
	web.Run(conn) // BLOCKING CALL
}

func logEverythingGET(ctx *web.Context, val string) string {

        var buffer bytes.Buffer
        buffer.WriteString("GET<br>")
        buffer.WriteString("uri: ")
        buffer.WriteString(val)
        buffer.WriteString("<br>Params:<br>")

        for key, value := range ctx.Params {
                s := fmt.Sprintf("Key: %s Value: %s<br>", key, value)
                buffer.WriteString(s)
        }
        return buffer.String()
}

func logEverythingPOST(ctx *web.Context, val string) string {

        var buffer bytes.Buffer
        buffer.WriteString("POST<br>")
        buffer.WriteString("uri: ")
        buffer.WriteString(val)
        buffer.WriteString("<br>Params:<br>")

        for key, value := range ctx.Params {
                s := fmt.Sprintf("Key: %s Value: %s<br>", key, value)
                buffer.WriteString(s)
        }

        //ctx.Abort(500, "This is just to show i can error shit!")

        return buffer.String()
}

///////////////////////////
// DB RELATED
//////////////////////////

// Define Schema here as Structs
type User struct {
  gorm.Model
  
  UID 			string
  Name 			string
  NumSessions	uint
}

func testDbAbility() {
	db, err := gorm.Open("mysql", "fbg:firebrand@tcp(fbgdb.car3xz0htwap.us-east-1.rds.amazonaws.com:3306)/gamedb?charset=utf8&parseTime=True&loc=Local")
	if (err != nil){
		fmt.Println("ERROR: " + err.Error());
	}
	defer db.Close()

	// Migrate the schema  (creates a users table following ruby db naming conventions)
	db.AutoMigrate(&User{})
	
	// Create
	db.Create(&User{UID: "ABCDEFG", Name: "Bryan", NumSessions: 7})
	
	// Read
	var user User
	db.First(&user, "uid = ?", "ABCDEFG") // find uses the db syntax uid vs UID

	// Update - update user's num_sessions to 8
	db.Model(&user).Update("NumSessions", 8)

	// Delete - delete u
	//db.Delete(&user)
}