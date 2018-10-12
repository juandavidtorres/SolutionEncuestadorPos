Public Class ColMangueras
    Implements IEnumerable

    Private mCol As Collection
    Private I As Integer = 0
    Public Function Add(idManguera As Long, IdProductoActivo As Integer, Precio As Double, Optional sKey As String = "0") As Manguera
        'crear un nuevo objeto
        Dim objNewMember As Manguera
        objNewMember = New Manguera
        I = I + 1
        sKey = I
        'establecer las propiedades que se transfieren al método
        objNewMember.idManguera = idManguera
        objNewMember.IdProductoActivo = IdProductoActivo
        objNewMember.Precio = Precio
        If Len(sKey) = 0 Then
            mCol.Add(objNewMember)
        Else
            mCol.Add(objNewMember, sKey)
        End If

        'devolver el objeto creado
        Add = objNewMember
        objNewMember = Nothing

    End Function

    Public ReadOnly Property Item(vntIndexKey As Object) As Manguera
        Get
            Return mCol(vntIndexKey)
        End Get
    End Property

    Public ReadOnly Property Items() As Collection
        Get
            Return mCol
        End Get
    End Property

    Public ReadOnly Property Count() As Long
        Get
            Return mCol.Count
        End Get
    End Property

    Public Sub Remove(vntIndexKey As Object)
        mCol.Remove(vntIndexKey)
    End Sub

    Sub New()
        'crea la colección cuando se crea la clase
        mCol = New Collection
    End Sub

    Public Function GetEnumerator() As IEnumerator _
      Implements IEnumerable.GetEnumerator

        Return New BaseEnum(mCol)
    End Function
End Class

Public Class BaseEnum
    Implements IEnumerator

    Private mCol As Collection

    ' Enumerators are positioned before the first element
    ' until the first MoveNext() call.
    Dim position As Integer = -1

    Public Sub New(ByVal mCol1 As Collection)
        mCol = mCol1
    End Sub

    Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
        position = position + 1
        Return (position < mCol.Count)
    End Function

    Public Sub Reset() Implements IEnumerator.Reset
        position = -1
    End Sub

    Public ReadOnly Property Current() As Object Implements IEnumerator.Current
        Get
            Try
                Return mCol(position)
            Catch ex As IndexOutOfRangeException
                Throw New InvalidOperationException()
            End Try
        End Get
    End Property
End Class