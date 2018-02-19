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


func main() {

	db, err := gorm.Open("mysql", "fbg:firebrand@fbgdb.car3xz0htwap.us-east-1.rds.amazonaws.com/gamedb?charset=utf8&parseTime=True&loc=Local")
	if (err != nil){
		fmt.Println("ERROR: " + err.Error());
	}
	defer db.Close()


    web.Get("/(.*)", logEverythingGET)
    web.Post("/(.*)", logEverythingPOST)
    web.Run("0.0.0.0:3000")
}

