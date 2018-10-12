Public Class ColCanastilla
    Implements IEnumerable

    'variable local para contener colección
    Private mCol As Collection

    Public Function Add(Codigo As String, Cantidad As Double, Isla As Integer, Optional sKey As String = "") As Canastilla
        'crear un nuevo objeto
        Dim objNewMember As Canastilla
        objNewMember = New Canastilla


        'establecer las propiedades que se transfieren al método
        objNewMember.Codigo = Codigo
        objNewMember.Cantidad = Cantidad
        objNewMember.Isla = Isla

        If Len(sKey) = 0 Then
            mCol.Add(objNewMember)
        Else
            mCol.Add(objNewMember, sKey)
        End If


        'devolver el objeto creado
        Add = objNewMember
        objNewMember = Nothing


    End Function

    Public ReadOnly Property Item(vntIndexKey As Object) As Canastilla
        Get
            Return mCol(vntIndexKey)
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



