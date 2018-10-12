Public Class ColMediosPago
    Implements IEnumerable

    'variable local para contener colección
    Private mCol As New Collection

    Public Function Add(IdMedioPago As Integer, Valor As String, Optional sKey As String = "") As MediosPago
        'crear un nuevo objeto
        Dim objNewMember As MediosPago
        objNewMember = New MediosPago


        'establecer las propiedades que se transfieren al método
        objNewMember.IdMedioPago = IdMedioPago
        objNewMember.Valor = Valor
        If Len(sKey) = 0 Then
            mCol.Add(objNewMember)
        Else
            mCol.Add(objNewMember, sKey)
        End If


        'devolver el objeto creado
        Add = objNewMember
        objNewMember = Nothing


    End Function

    Public ReadOnly Property Item(vntIndexKey As Object) As MediosPago
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
