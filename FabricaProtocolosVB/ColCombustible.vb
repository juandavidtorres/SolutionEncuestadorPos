Public Class ColCombustible
    Implements IEnumerable

    'variable local para contener colección
    Private mCol As New Collection
    Private I As Integer = 0

    Public Function Add(Valor As String, Descripcion As String, Optional skey As String = "") As RecCombustible
        'crear un nuevo objeto
        Dim objNewMember As RecCombustible
        objNewMember = New RecCombustible
        I = I + 1
        skey = I
        'establecer las propiedades que se transfieren al método
        objNewMember.Descripcion = Descripcion
        objNewMember.Valor = Valor
        
        If skey.Length = 0 Then
            mCol.Add(objNewMember)
        Else
            mCol.Add(objNewMember, skey)
        End If

        'devolver el objeto creado
        Add = objNewMember
        objNewMember = Nothing

    End Function

    Public ReadOnly Property Item(vntIndexKey As Object) As TanqueInventario
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
