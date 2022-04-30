USE [master]
/****** Object:  Database [Samples.AspNetCore5]    Script Date: 5/20/2021 5:18:35 PM ******/
CREATE DATABASE [Samples.AspNetCore5]   
 CONTAINMENT = NONE

ALTER DATABASE [Samples.AspNetCore5] SET COMPATIBILITY_LEVEL = 130

begin
EXEC [Samples.AspNetCore5].[dbo].[sp_fulltext_database] @action = 'enable'
end
