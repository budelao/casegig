using CaseGig.Domain.Enums;

namespace CaseGig.Domain.Entities;

public sealed class Ordem
{
    public Guid IdOrdem { get; set; }
    public Guid IdCliente { get; set; }
    public Guid IdFundo { get; set; }
    public TipoOperacao TipoOperacao { get; set; }
    public decimal QuantidadeCotas { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime? DataAgendamento { get; set; }
    public DateTime? DataProcessamento { get; set; }
    public StatusOrdem Status { get; set; }
    public long RowVersion { get; set; }

    public Cliente? Cliente { get; set; }
    public Fundo? Fundo { get; set; }
}
