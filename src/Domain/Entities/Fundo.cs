using CaseGig.Domain.Enums;

namespace CaseGig.Domain.Entities;

public sealed class Fundo
{
    public Guid IdFundo { get; set; }
    public string Nome { get; set; } = string.Empty;
    public TimeSpan HorarioCorte { get; set; }
    public decimal ValorCota { get; set; }
    public decimal ValorMinimoAporte { get; set; }
    public decimal ValorMinimoPermanencia { get; set; }
    public StatusCaptacao StatusCaptacao { get; set; }
    public long RowVersion { get; set; }

    public ICollection<Posicao> Posicoes { get; set; } = new List<Posicao>();
    public ICollection<Ordem> Ordens { get; set; } = new List<Ordem>();
}
