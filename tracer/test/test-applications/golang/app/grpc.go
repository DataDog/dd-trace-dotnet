package main

import (
	"context"
	"fmt"
	"io"
	"log"
	"net"

	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/protobuf/types/known/structpb"

	grpctrace "gopkg.in/DataDog/dd-trace-go.v1/contrib/google.golang.org/grpc"
)

func listenAndServeGRPC() {
	lis, err := net.Listen("tcp", ":7778")
	if err != nil {
		log.Fatalf("failed to listen: %v", err)
	}
	grpcServer := grpc.NewServer(
		grpc.UnaryInterceptor(grpctrace.UnaryServerInterceptor(grpctrace.WithRequestTags())),
		grpc.StreamInterceptor(grpctrace.StreamServerInterceptor()),
		grpc.Creds(insecure.NewCredentials()),
	)
	RegisterWeblogServer(grpcServer, server{})
	if err := grpcServer.Serve(lis); err != nil {
		log.Fatal(err)
	}
}

type server struct {
	UnimplementedWeblogServer
}

func (s server) Unary(ctx context.Context, req *structpb.Value) (*structpb.Value, error) {
	return structpb.NewStringValue("hello from Go"), nil
}

func (s server) ServerStream(req *structpb.Value, stream Weblog_ServerStreamServer) error {
	for c := 0; c < 10; c++ {
		err := stream.Send(structpb.NewStringValue(fmt.Sprintf("hello from grpc go server %d", c)))
		if err == io.EOF {
			break
		}
		if err != nil {
			return err
		}
	}
	return nil
}

func (s server) ClientStream(stream Weblog_ClientStreamServer) error {
	for {
		_, err := stream.Recv()
		if err == io.EOF {
			return stream.SendAndClose(structpb.NewStringValue("hello from grpc go server"))
		}
		if err != nil {
			return err
		}
	}
}

func (s server) Bidi(stream Weblog_BidiServer) error {
	for {
		_, err := stream.Recv()
		if err == io.EOF {
			return stream.Send(structpb.NewStringValue("hello from grpc go server"))
		}
		if err != nil {
			return err
		}
	}
}
