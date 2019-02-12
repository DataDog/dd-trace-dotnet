<%@ Page Title="Datadog Coffeehouse" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeFile="Default.aspx.cs" Inherits="Samples.WebForms._Default" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">

    <div class="jumbotron">
        <h1>Datadog WebForms Coffeehouse Demo</h1>
        <p class="lead">Welcome to the Datadog Coffeehouse demo WebForms App.</p>
        <p><a href="https://docs.datadoghq.com/tracing/languages/dotnet/?tab=netframeworkonwindows" class="btn btn-primary btn-lg">Learn more about Tracing &raquo;</a></p>
    </div>

    <div class="row">
        <div class="col-md-4">
            <h2>Browse Products</h2>
            <p>
                Go browse the products we offer.
            </p>
            <p>
                <a class="btn btn-default" href="/ProductList">Browse &raquo;</a>
            </p>
        </div>
    </div>
</asp:Content>
