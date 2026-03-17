<%@ Page Title="Home" Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Samples.WebForms.Ninject._Default" %>

<!DOCTYPE html>
<html>
<head runat="server">
    <title>WebForms Ninject Sample</title>
</head>
<body>
    <form id="form1" runat="server">
        <h1>WebForms + Ninject Test Application</h1>
        <asp:Repeater ID="rptItems" runat="server">
            <HeaderTemplate><ul></HeaderTemplate>
            <ItemTemplate>
                <li><%# Container.DataItem %></li>
            </ItemTemplate>
            <FooterTemplate></ul></FooterTemplate>
        </asp:Repeater>
    </form>
</body>
</html>
