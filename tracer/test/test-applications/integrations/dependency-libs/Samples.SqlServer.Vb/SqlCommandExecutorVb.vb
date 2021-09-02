Imports System.Data
Imports System.Data.SqlClient
Imports System.Threading
Imports Samples.DatabaseHelper

Public Class SqlCommandExecutorVb
    Inherits DbCommandExecutor(Of SqlCommand)

    Public Overrides ReadOnly Property CommandTypeName As String = "SqlCommandVb"
    Public Overrides ReadOnly Property SupportsAsyncMethods As Boolean = True

    Public Overrides Sub ExecuteNonQuery(command As SqlCommand)
        command.ExecuteNonQuery()
    End Sub

    Public Overrides Function ExecuteNonQueryAsync(command As SqlCommand) As Task
        Return command.ExecuteNonQueryAsync()
    End Function

    Public Overrides Function ExecuteNonQueryAsync(command As SqlCommand, cancellationToken As CancellationToken) As Task
        Return command.ExecuteNonQueryAsync(cancellationToken)
    End Function

    Public Overrides Sub ExecuteScalar(command As SqlCommand)
        command.ExecuteScalar()
    End Sub

    Public Overrides Function ExecuteScalarAsync(command As SqlCommand) As Task
        Return command.ExecuteScalarAsync()
    End Function

    Public Overrides Function ExecuteScalarAsync(command As SqlCommand, cancellationToken As CancellationToken) As Task
        Return command.ExecuteScalarAsync(cancellationToken)
    End Function

    Public Overrides Sub ExecuteReader(command As SqlCommand)
        Using command.ExecuteReader()
        End Using
    End Sub

    Public Overrides Sub ExecuteReader(command As SqlCommand, behavior As CommandBehavior)
        Using command.ExecuteReader(behavior)
        End Using
    End Sub

    Public Overrides Async Function ExecuteReaderAsync(command As SqlCommand) As Task
        Using Await command.ExecuteReaderAsync()
        End Using
    End Function

    Public Overrides Async Function ExecuteReaderAsync(command As SqlCommand, behavior As CommandBehavior) As Task
        Using Await command.ExecuteReaderAsync(behavior)
        End Using
    End Function

    Public Overrides Async Function ExecuteReaderAsync(command As SqlCommand, cancellationToken As CancellationToken) As Task
        Using Await command.ExecuteReaderAsync(cancellationToken)
        End Using
    End Function

    Public Overrides Async Function ExecuteReaderAsync(command As SqlCommand, behavior As CommandBehavior, cancellationToken As CancellationToken) As Task
        Using Await command.ExecuteReaderAsync(behavior, cancellationToken)
        End Using
    End Function
End Class
