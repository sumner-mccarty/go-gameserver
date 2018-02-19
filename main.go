package main

import (
        "fmt"
        "bytes"
        "github.com/hoisie/web"
		"github.com/jinzhu/gorm"
		 _ "github.com/jinzhu/gorm/dialects/mysql"
)

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


type User struct {
  gorm.Model
  
  UID 			string
  Name 			string
  NumSessions	uint
}

func main() {

	db, err := gorm.Open("mysql", "fbg:firebrand@tcp(fbgdb.car3xz0htwap.us-east-1.rds.amazonaws.com:3306)/gamedb?charset=utf8&parseTime=True&loc=Local")
	if (err != nil){
		fmt.Println("ERROR: " + err.Error());
	}
	defer db.Close()

	// Migrate the schema
	db.AutoMigrate(&User{})
	
	// Create
	db.Create(&User{UID: "ABCDEFG", Name: "Bryan", NumSessions: 7})
	
	// Read
	var user User
	db.First(&user, "uid = ?", "ABCDEFG") // find uses the db syntax uid vs UID

	// Update - update user's num_sessions to 8
	db.Model(&user).Update("NumSessions", 8)

	// Delete - delete product
	//db.Delete(&product)

	
    //web.Get("/(.*)", logEverythingGET)
    //web.Post("/(.*)", logEverythingPOST)
    //web.Run("0.0.0.0:3000")
}

