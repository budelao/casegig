namespace CaseGig.Domain.Entities;

public sealed class Cliente
{
    public Guid IdCliente { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public decimal SaldoDisponivel { get; set; }
    public long RowVersion { get; set; }

    public ICollection<Posicao> Posicoes { get; set; } = new List<Posicao>();
    public ICollection<Ordem> Ordens { get; set; } = new List<Ordem>();

    public void DebitarSaldo(decimal valor)
    {
        if (valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor));
        }

        SaldoDisponivel -= valor;
    }

    public void CreditarSaldo(decimal valor)
    {
        if (valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor));
        }

        SaldoDisponivel += valor;
    }
}
