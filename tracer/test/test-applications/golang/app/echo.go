package main

import (
	"net/http"
	"strconv"

	"github.com/labstack/echo/v4"

	"gopkg.in/DataDog/dd-trace-go.v1/appsec"
	echotrace "gopkg.in/DataDog/dd-trace-go.v1/contrib/labstack/echo.v4"
	"gopkg.in/DataDog/dd-trace-go.v1/ddtrace/tracer"
)

func main() {
	tracer.Start()
	defer tracer.Stop()

	r := echo.New()

	r.Use(echotrace.Middleware())

	r.Any("/", func(c echo.Context) error {
		return c.NoContent(http.StatusOK)
	})

	r.Any("/*", func(c echo.Context) error {
		return c.NoContent(http.StatusNotFound)
	})

	r.Any("/waf", waf)
	r.Any("/waf/", waf)

	r.Any("/sample_rate_route/:i", func(c echo.Context) error {
		return c.String(http.StatusOK, "OK")
	})

	r.Any("/params/:i", func(c echo.Context) error {
		return c.String(http.StatusOK, "OK")
	})

	r.Any("/status", func(c echo.Context) error {
		rCode := 200
		if codeStr := c.Request().URL.Query().Get("code"); codeStr != "" {
			if code, err := strconv.Atoi(codeStr); err == nil {
				rCode = code
			}
		}
		return c.String(rCode, "OK")
	})

	r.Any("/headers/", headers)
	r.Any("/headers", headers)

	identify := func(c echo.Context) error {
		if span, ok := tracer.SpanFromContext(c.Request().Context()); ok {
			tracer.SetUser(
				span, "usr.id", tracer.WithUserEmail("usr.email"),
				tracer.WithUserName("usr.name"), tracer.WithUserSessionID("usr.session_id"),
				tracer.WithUserRole("usr.role"), tracer.WithUserScope("usr.scope"),
			)
		}
		return c.String(http.StatusOK, "Hello, identify!")
	}
	r.Any("/identify/", identify)
	r.Any("/identify", identify)
	r.Any("/identify-propagate", func(c echo.Context) error {
		if span, ok := tracer.SpanFromContext(c.Request().Context()); ok {
			tracer.SetUser(span, "usr.id", tracer.WithPropagation())
		}
		return c.String(http.StatusOK, "Hello, identify-propagate!")
	})

	initDatadog()
	go listenAndServeGRPC()
	r.Start(":7777")
}

func headers(c echo.Context) error {
	//Data used for header content is irrelevant here, only header presence is checked
	c.Response().Writer.Header().Set("content-type", "text/plain")
	c.Response().Writer.Header().Set("content-length", "42")
	c.Response().Writer.Header().Set("content-language", "en-US")

	return c.String(http.StatusOK, "Hello, headers!")
}

func waf(c echo.Context) error {
	req := c.Request()
	body, err := parseBody(req)
	if err == nil {
		appsec.MonitorParsedHTTPBody(req.Context(), body)
	}
	return c.String(http.StatusOK, "Hello, WAF!\n")
}
