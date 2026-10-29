Imports Npgsql
Imports System.Data

Public Class ProductService
    ' クラス内部で使い回す接続文字列の変数
    Private ReadOnly _connStr As String

    ''' <summary>
    ''' コンストラクタ：C#のコントローラーから安全に初期化された接続文字列を受け取ります
    ''' </summary>
    Public Sub New(connStr As String)
        _connStr = connStr
    End Sub

    ''' <summary>
    ''' 1. 全作品の一覧とバージョン数をポスグレから一撃で取得します
    ''' </summary>
    Public Function GetAllProducts() As DataTable
        Dim dt As New DataTable()
        ' ★ 英語名テーブルと列名に修正し、件数を「version_count」として取得します
        Dim sql As String =
            "SELECT m.product_id, m.product_name, COUNT(v.version_no) AS version_count " &
            "FROM m_products m " &
            "LEFT JOIN t_product_versions v ON m.product_id = v.product_id " &
            "GROUP BY m.product_id, m.product_name " &
            "ORDER BY m.product_id;"

        Using conn As New NpgsqlConnection(_connStr)
            Using cmd As New NpgsqlCommand(sql, conn)
                Dim adapter As New NpgsqlDataAdapter(cmd)
                adapter.Fill(dt)
            End Using
        End Using
        Return dt
    End Function

    ''' <summary>
    ''' 2. 指定された作品IDとバージョンNoに紐付く可変明細（仕様・更新履歴）を取得します
    ''' </summary>
    Public Function GetProgramDetails(progId As String, versionNo As Integer) As DataTable
        Dim dt As New DataTable()
        ' ★ 明細テーブル（t_product_version_details）から、項目名(detail_key)と値(detail_value)を取得します
        Dim sql As String = "SELECT detail_key, detail_value FROM t_product_version_details WHERE product_id = @PID AND version_no = @VNo ORDER BY detail_seq"

        Using conn As New NpgsqlConnection(_connStr)
            Using cmd As New NpgsqlCommand(sql, conn)
                ' C#側から送られてきた引数をSQLのパラメータ（@）に安全にセット
                cmd.Parameters.AddWithValue("@PID", progId)
                cmd.Parameters.AddWithValue("@VNo", versionNo)

                Dim adapter As New NpgsqlDataAdapter(cmd)
                adapter.Fill(dt)
            End Using
        End Using
        Return dt
    End Function

    ''' <summary>
    ''' 3. 指定されたバージョンの画像バイナリ（画面スクショ）をポスグレから取得します
    ''' </summary>
    Public Function GetProductImageBytes(productId As String, versionNo As Integer) As Byte()
        ' ★ 画像カラム（product_image）をターゲットに取得します
        Dim sql As String = "SELECT product_image FROM t_product_versions WHERE product_id = @PID AND version_no = @VNo"

        Using conn As New NpgsqlConnection(_connStr)
            Using cmd As New NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("@PID", productId)
                cmd.Parameters.AddWithValue("@VNo", versionNo)

                conn.Open()
                Dim result As Object = cmd.ExecuteScalar()

                ' データが実在し、NULLでなければバイト配列として返却
                If result IsNot Nothing AndAlso result IsNot DBNull.Value Then
                    Return CType(result, Byte())
                End If
            End Using
        End Using
        Return Nothing
    End Function

    ''' <summary>
    ''' 4. C#側から渡された画面スクショのバイト配列を、該当バージョンのポスグレカラムへUPDATE保存します
    ''' </summary>
    Public Sub SaveProductImage(productId As String, versionNo As Integer, imageBytes As Byte())

        ' ★ ここに UPDATE文 が定義されています
        Dim sql As String = "UPDATE t_product_versions SET product_image = @Image WHERE product_id = @PID AND version_no = @VNo"

        Using conn As New NpgsqlConnection(_connStr)
            Using cmd As New NpgsqlCommand(sql, conn)

                ' C#側から送られてきた写真バイナリ（バイト配列）や製品IDをパラメータにセット
                cmd.Parameters.Add("@Image", NpgsqlTypes.NpgsqlDbType.Bytea).Value = imageBytes
                cmd.Parameters.AddWithValue("@PID", productId)
                cmd.Parameters.AddWithValue("@VNo", versionNo)

                conn.Open()

                ' ★★★ この命令が走った瞬間に、ポスグレ側で実際にUPDATEが実行されます！ ★★★
                cmd.ExecuteNonQuery()

            End Using
        End Using
    End Sub

    ' 【VB.NET側】ProductService.vb の中に追加
    ''' <summary>
    ''' 5. 指定された作品IDに紐付くすべてのバージョン一覧（Noと名称）を動的に取得します
    ''' </summary>
    Public Function GetProductVersions(productId As String) As DataTable
        Dim dt As New DataTable()
        Dim sql As String = "SELECT version_no, version_name FROM t_product_versions WHERE product_id = @PID ORDER BY version_no"

        Using conn As New NpgsqlConnection(_connStr)
            Using cmd As New NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("@PID", productId)
                Dim adapter As New NpgsqlDataAdapter(cmd)
                adapter.Fill(dt)
            End Using
        End Using
        Return dt
    End Function

End Class
