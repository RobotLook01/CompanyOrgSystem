Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports Npgsql
Imports Dapper

Namespace ChatLogic

    Public Class ChatService
        Private ReadOnly _connectionString As String

        Public Sub New(connectionString As String)
            _connectionString = connectionString
        End Sub

        ''' <summary>
        ''' 1. タイムラインデータを取得（ツリー構造組み立て）
        ''' </summary>
        Public Function GetMessageTree() As List(Of ChatMessageDto)
            Using conn As New NpgsqlConnection(_connectionString)

                ' ⭕ SQLのSELECT句に「created_at AS CreatedAt」を追加します
                Dim sql As String = "SELECT message_id AS MessageId, parent_id AS ParentId, sender_name AS SenderName, content_markdown AS ContentMarkdown, created_at AS CreatedAt FROM chat_messages ORDER BY created_at ASC;"
                'Dim sql As String = "SELECT message_id AS MessageId, parent_id AS ParentId, sender_name AS SenderName, content_markdown AS ContentMarkdown FROM chat_messages ORDER BY created_at ASC;"

                Dim allMessages = conn.Query(Of ChatMessageDto)(sql).ToList()
                Dim messageMap = New Dictionary(Of Integer, ChatMessageDto)()
                Dim rootNodes = New List(Of ChatMessageDto)()

                For Each msg In allMessages
                    If Not messageMap.ContainsKey(msg.MessageId) Then
                        messageMap.Add(msg.MessageId, msg)
                    End If
                Next

                For Each msg In allMessages
                    If msg.ParentId.HasValue Then
                        If messageMap.ContainsKey(msg.ParentId.Value) Then
                            messageMap(msg.ParentId.Value).Replies.Add(msg)
                        End If
                    Else
                        rootNodes.Add(msg)
                    End If
                Next

                Return rootNodes
            End Using
        End Function

        ''' <summary>
        ''' ★【新設】2. 過去の投稿内容を変更（UPDATE）します
        ''' </summary>
        Public Sub UpdateMessage(messageId As Integer, contentMarkdown As String)
            Using conn As New NpgsqlConnection(_connectionString)
                Dim sql As String = "UPDATE chat_messages SET content_markdown = @ContentMarkdown WHERE message_id = @MessageId;"
                conn.Execute(sql, New With {.MessageId = messageId, .ContentMarkdown = contentMarkdown})
            End Using
        End Sub

        ''' <summary>
        ''' ★【新設】3. 過去の投稿を削除（DELETE）します
        ''' DDLの CASCADE 設定により、ぶら下がる返信コメントも自動消去されます
        ''' </summary>
        Public Sub DeleteMessage(messageId As Integer)
            Using conn As New NpgsqlConnection(_connectionString)
                Dim sql As String = "DELETE FROM chat_messages WHERE message_id = @MessageId;"
                conn.Execute(sql, New With {.MessageId = messageId})
            End Using
        End Sub
    End Class

    Public Class ChatMessageDto
        Public Property MessageId As Integer
        Public Property ParentId As Integer?
        Public Property SenderName As String
        Public Property ContentMarkdown As String

        ' ⭕ 【ここを追加！】C#へ日時を受け渡すためのDateTime型プロパティです
        Public Property CreatedAt As DateTime

        Public Property Replies As New List(Of ChatMessageDto)()
    End Class

End Namespace
