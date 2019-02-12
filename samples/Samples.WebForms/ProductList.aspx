<%@ Page Title="Products" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="ProductList.aspx.cs" Inherits="Samples.WebForms.ProductList" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" Runat="Server">
    <section>
        <div>
            <asp:ListView ID="productList" runat="server" 
                DataKeyNames="Id" GroupItemCount="4"
                ItemType="Samples.WebForms.Product" SelectMethod="GetProducts">
                <EmptyDataTemplate>
                    <table >
                        <tr>
                            <td>No data was returned.</td>
                        </tr>
                    </table>
                </EmptyDataTemplate>
                <EmptyItemTemplate>
                    <td/>
                </EmptyItemTemplate>
                <GroupTemplate>
                    <tr id="itemPlaceholderContainer" runat="server">
                        <td id="itemPlaceholder" runat="server"></td>
                    </tr>
                </GroupTemplate>
                <ItemTemplate>
                    <td runat="server">
                        <table>
                            <tr>
                                <td>
                                  <a href="<%#: GetRouteUrl("ProductByNameRoute", new {productName = Item.Name}) %>">
                                  </a>
                                </td>
                            </tr>
                            <tr>
                                <td>
                                    <a href="<%#: GetRouteUrl("ProductByNameRoute", new {productName = Item.Name}) %>">
                                      <%#:Item.Name%>
                                    </a>
                                    <br />
                                    <span>
                                        <b>Price: </b><%#:string.Format("{0:c}", Item.UnitCost)%>
                                    </span>
                                    <br />
                                    <%
                                    if (IsAuthenticated())
                                    {
                                    %>
                                        <a href="/PlaceOrder.aspx?productId=<%#:Item.Id %>">
                                            <span class="ProductListItem">
                                                <b>Order Now<b>
                                            </span>           
                                        </a>
                                    <%
                                    }
                                    else
                                    {
                                    %>
                                        <a href="/Account/Login">
                                            <span class="ProductListItem">
                                                <b>Login to order<b>
                                            </span>           
                                        </a>
                                    <%
                                    }
                                    %>    
                                </td>
                            </tr>
                            <tr>
                                <td>&nbsp;</td>
                            </tr>
                        </table>
                        </p>
                    </td>
                </ItemTemplate>
                <LayoutTemplate>
                    <table style="width:100%;">
                        <tbody>
                            <tr>
                                <td>
                                    <table id="groupPlaceholderContainer" runat="server" style="width:100%">
                                        <tr id="groupPlaceholder"></tr>
                                    </table>
                                </td>
                            </tr>
                            <tr>
                                <td></td>
                            </tr>
                            <tr></tr>
                        </tbody>
                    </table>
                </LayoutTemplate>
            </asp:ListView>
        </div>
    </section>
</asp:Content>

