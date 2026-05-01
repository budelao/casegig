namespace CaseGig.Domain.Entities;

public sealed class Posicao
{
    public Guid IdCliente { get; set; }
    public Guid IdFundo { get; set; }
    public decimal QuantidadeCotas { get; set; }
    public long RowVersion { get; set; }

    public Cliente? Cliente { get; set; }
    public Fundo? Fundo { get; set; }

    public void DebitarCotas(decimal quantidade)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade));
        }

        QuantidadeCotas -= quantidade;
    }

    public void CreditarCotas(decimal quantidade)
    {
        if (quantidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantidade));
        }

        QuantidadeCotas += quantidade;
    }
}
