package main

import (
	"net/http"
	"strconv"

	"github.com/gin-gonic/gin"

	"gopkg.in/DataDog/dd-trace-go.v1/appsec"
	gintrace "gopkg.in/DataDog/dd-trace-go.v1/contrib/gin-gonic/gin"
	"gopkg.in/DataDog/dd-trace-go.v1/ddtrace/tracer"
	"fmt"
)

func main() {
	tracer.Start()
	defer tracer.Stop()

	r := gin.New()
	r.Use(gintrace.Middleware("weblog"))

	r.Any("/", func(ctx *gin.Context) {
		ctx.Writer.WriteHeader(http.StatusOK)
	})
	r.Any("/waf", func(ctx *gin.Context) {
		body, err := parseBody(ctx.Request)
		if err == nil {
			appsec.MonitorParsedHTTPBody(ctx.Request.Context(), body)
		}
		ctx.Writer.Write([]byte("Hello, WAF!\n"))
	})
	r.Any("/waf/*allpaths", func(ctx *gin.Context) {
		ctx.Writer.Write([]byte("Hello, WAF!\n"))
	})
	r.Any("/sample_rate_route/:i", func(ctx *gin.Context) {
		ctx.Writer.Write([]byte("OK"))
	})
	r.Any("/params/:myParam", func(ctx *gin.Context) {
		ctx.Writer.Write([]byte("OK"))
	})

	r.Any("/status", func(ctx *gin.Context) {
		if c := ctx.Request.URL.Query().Get("code"); c != "" {
			if code, err := strconv.Atoi(c); err == nil {
				ctx.Writer.WriteHeader(code)
			}
		}
		ctx.Writer.Write([]byte("OK"))
	})

	r.Any("/headers/", headers)
	r.Any("/headers", headers)

	identify := func(ctx *gin.Context) {
		if span, ok := tracer.SpanFromContext(ctx.Request.Context()); ok {
			tracer.SetUser(
				span, "usr.id", tracer.WithUserEmail("usr.email"),
				tracer.WithUserName("usr.name"), tracer.WithUserSessionID("usr.session_id"),
				tracer.WithUserRole("usr.role"), tracer.WithUserScope("usr.scope"),
			)
		}
		ctx.Writer.Write([]byte("Hello, identify!"))
	}
	r.Any("/identify/", identify)
	r.Any("/identify", identify)
	r.Any("/identify-propagate", func(ctx *gin.Context) {
		if span, ok := tracer.SpanFromContext(ctx.Request.Context()); ok {
			tracer.SetUser(span, "usr.id", tracer.WithPropagation())
		}
		ctx.Writer.Write([]byte("Hello, identify-propagate!"))
	})

	initDatadog()
	go listenAndServeGRPC()
	fmt.Println("Now listening on: 7777")
	http.ListenAndServe(":7777", r)
}

func headers(ctx *gin.Context) {
	//Data used for header content is irrelevant here, only header presence is checked
	ctx.Writer.Header().Set("content-type", "text/plain")
	ctx.Writer.Header().Set("content-length", "42")
	ctx.Writer.Header().Set("content-language", "en-US")
	ctx.Writer.Write([]byte("Hello, headers!"))
}
