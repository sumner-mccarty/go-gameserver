package main

import (
        "fmt"
        "bytes"
        "github.com/hoisie/web"
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
    web.Get("/(.*)", logEverythingGET)
    web.Post("/(.*)", logEverythingPOST)
    web.Run("0.0.0.0:3000")
}

