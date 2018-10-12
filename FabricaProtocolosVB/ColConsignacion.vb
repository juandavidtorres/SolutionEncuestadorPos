Public Class ColConsignacion
    Implements IEnumerable

    'variable local para contener colección
    Private mCol As New Collection


    Public Function Add(IdTipo As Integer, Valor As Double, TipoMoneda As Integer) As Consignacion
        'crear un nuevo objeto
        Dim objNewMember As Consignacion
        objNewMember = New Consignacion


        'establecer las propiedades que se transfieren al método
        objNewMember.IdTipo = IdTipo
        objNewMember.Valor = Valor
        objNewMember.TipoMoneda = TipoMoneda

        mCol.Add(objNewMember)

        'devolver el objeto creado
        Add = objNewMember
        objNewMember = Nothing
    End Function


    Public ReadOnly Property Item(vntIndexKey As Object) As Consignacion
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