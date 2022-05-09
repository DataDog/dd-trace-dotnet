<%@ Page Title="Health" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Health.aspx.cs" Inherits="Samples.Security.WebForms._Default" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">

    <div>Health</div>

    <asp:TextBox runat="server" ID="testBox" />
    <asp:Button runat="server" Text="Submit" />
    <br />
    <label>Route datas are: <%= Page.RouteData.Values["id"]%></label>
</asp:Content>

