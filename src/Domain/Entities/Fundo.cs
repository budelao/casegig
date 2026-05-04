using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;

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

    public void GarantirAbertoParaOperacoes()
    {
        if (StatusCaptacao != StatusCaptacao.ABERTO)
        {
            throw new BusinessRuleException("Fundo está FECHADO para novas operações.");
        }
    }

    public void GarantirDentroDoCutoff(DateTime agora)
    {
        if (agora.TimeOfDay > HorarioCorte)
        {
            throw new BusinessRuleException("Fora da janela de cut-off. Para operações futuras, utilize o endpoint de agendamento.");
        }
    }

    public void GarantirDataAgendamentoValida(DateOnly dataAgendamento, DateTime agora)
    {
        var hoje = DateOnly.FromDateTime(agora.Date);
        if (dataAgendamento <= hoje)
        {
            throw new BusinessRuleException("Data de agendamento deve ser futura (D+1 ou adiante).");
        }

        if (dataAgendamento.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            throw new BusinessRuleException("Data de agendamento deve ser um dia útil (segunda a sexta).");
        }
    }

    public DateTime CalcularDataExecucao(DateOnly dataAgendamento)
    {
        return dataAgendamento.ToDateTime(TimeOnly.FromTimeSpan(HorarioCorte));
    }
}
