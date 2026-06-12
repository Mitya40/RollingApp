using RollingApp.Models.Calculation;
using RollingApp.Models.Domain;

using System;
using System.Collections.Generic;
using System.Linq;

namespace RollingApp.Services
{
    public class CalculationEngineService
    {
        private double Rnd3(double val) => Math.Round(val, 3, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Точка входа в математическое ядро. 
        /// Инициализирует контекст вычислений, загружает данные из моделей БД, 
        /// запускает расчет по выбранному методу и возвращает заполненный контекст с результатами.
        /// </summary>
        public CalculationContext RunCalculation(InitialParameter initParams, List<Pass> passes, List<Stand> stands, Steel steel, CalculationSettings settings)
        {
            var ctx = new CalculationContext();

            // 1. Инициализация глобальных переменных
            ctx.M = steel.M;
            ctx.W0 = initParams.W0;
            ctx.P0 = initParams.P0;
            ctx.L0 = initParams.L0;
            ctx.T0 = initParams.T0;
            ctx.TAU = initParams.TAU;
            ctx.LR = initParams.LR;
            ctx.VK = initParams.VK;

            ctx.K1 = steel.K1; ctx.K2 = steel.K2; ctx.K3 = steel.K3;
            ctx.K4 = steel.K4; ctx.K5 = steel.K5; ctx.K6 = steel.K6;
            ctx.K7 = steel.K7; ctx.K8 = steel.K8; ctx.K9 = steel.K9;

            ctx.K10 = settings.K10; ctx.K11 = settings.K11; ctx.K12 = settings.K12;
            ctx.K13 = settings.K13; ctx.K14 = settings.K14; ctx.K15 = settings.K15;
            ctx.K16 = settings.K16; ctx.K17 = settings.K17; ctx.TS = settings.TS;

            // 2. Инициализация массивов [1..NPR]
            var orderedPasses = passes.OrderBy(p => p.N).ToList();
            ctx.NPR = orderedPasses.Count;

            for (int i = 0; i < ctx.NPR; i++)
            {
                int idx = i + 1; // Индексация с 1
                var p = orderedPasses[i];
                var s = stands.FirstOrDefault(st => st.SequenceNumber == p.N);

                ctx.N[idx] = p.N;
                ctx.KOD0[idx] = p.KOD0;
                ctx.KOD1[idx] = p.KOD1;

                // Бэкап оригинальных кодов до нормализации
                ctx.KOD0D[idx] = p.KOD0;
                ctx.KOD1D[idx] = p.KOD1;

                ctx.H0[idx] = Rnd3(p.H0); ctx.B0[idx] = Rnd3(p.B0);
                ctx.H1[idx] = Rnd3(p.H1); ctx.B1[idx] = Rnd3(p.B1);
                ctx.W[idx] = p.W; ctx.S[idx] = Rnd3(p.S);
                ctx.BVR[idx] = Rnd3(p.BVR); ctx.BD[idx] = Rnd3(p.BD);
                ctx.R[idx] = Rnd3(p.R); ctx.ROV[idx] = Rnd3(p.ROV);
                ctx.R8[idx] = p.R8; ctx.SUMX[idx] = p.SUMX;
                ctx.PSI[idx] = p.PSI; ctx.Z[idx] = p.Z;
                ctx.SP[idx] = p.SP; ctx.TOP[idx] = p.TOP;

                if (s != null)
                {
                    ctx.DB[idx] = s.DB; ctx.DSH[idx] = s.DSH;
                    ctx.MU[idx] = s.MU; ctx.FPOD[idx] = s.FPOD;
                    ctx.LKL[idx] = s.LKL; ctx.AKL[idx] = s.AKL;
                    ctx.IR[idx] = s.IR; ctx.ETA[idx] = s.ETA;
                    ctx.NZ[idx] = s.NZ; ctx.NNOM[idx] = s.NNOM;
                    ctx.PP[idx] = s.PP; ctx.NDVN[idx] = s.NDVN;
                    ctx.NDVMIN[idx] = s.NDVMIN; ctx.NDVMAX[idx] = s.NDVMAX;
                    ctx.PDOP[idx] = s.PDOP; ctx.MDOP[idx] = s.MDOP;
                    ctx.C[idx] = s.C;
                }
            }

            // 3. Вычисление P0 (Периметр), если не задан, как в DataToProgramm
            if (ctx.NPR > 0 && Math.Abs(ctx.P0) < 0.001)
            {
                if (ctx.KOD0[1] == 1 || ctx.KOD0[1] == 8 || ctx.KOD0[1] == 11 || ctx.KOD0[1] == 12) ctx.P0 = 2 * (ctx.H0[1] + ctx.B0[1]);
                if (ctx.KOD0[1] == 2) ctx.P0 = ctx.PI * ctx.H0[1];
                if (ctx.KOD0[1] == 3) ctx.P0 = 2 * ctx.H0[1] * 1.414;
                if (ctx.KOD0[1] == 7) ctx.P0 = 2 * Math.Sqrt(ctx.H0[1] * ctx.H0[1] + ctx.B0[1] * ctx.B0[1]);
                if (ctx.KOD0[1] == 4) ctx.P0 = 2 * Math.Sqrt(ctx.H0[1] * ctx.H0[1] + 1.333 * ctx.B0[1] * ctx.B0[1]);
                if (ctx.KOD0[1] == 6) ctx.P0 = 2 * (ctx.H0[1] + 0.414 * ctx.B0[1]);
                if (ctx.KOD0[1] == 5) ctx.P0 = ctx.PI * ctx.B0[1] + 2 * (ctx.H0[1] - ctx.B0[1]);
                if (ctx.KOD0[1] == 9) ctx.P0 = 2 * Math.Sqrt(ctx.B0[1] * ctx.B0[1] + 1.333 * ctx.H0[1] * ctx.H0[1]);
            }

            ctx.par_0_data();

            // 4. Нормализация данных и проверка на физические ошибки
            bool hasCriticalErrors = ctx.Analis_data();
            if (hasCriticalErrors)
            {
                return ctx; // Возвращаем контекст с ошибками, дальше не считаем
            }

            // 5. Основная логика вычислений
            if (settings.CalculationMethod == 1)
            {
                // Метод кафедры ОМД
                ctx.domain();
            }
            else if (settings.CalculationMethod == 2)
            {
                // Метод соответственной полосы
                ctx.domainSP();
            }

            // 6. Финализация и подготовка данных для графиков
            ctx.Zazor();
            ctx.DataFromProgramm(); // Вычисляет средние значения и разворачивает NDVR_

            return ctx;
        }
    }

    // ========================================================================
    // СПРАВОЧНИК КОДОВ ФОРМ ПОДКАТОВ (KOD0) И КАЛИБРОВ (KOD1)
    // ========================================================================
    // 0, 1 : Гладкая бочка (прямоугольный подкат / полоса)
    // 2    : Круглый калибр
    // 3    : Квадратный калибр
    // 4    : Овальный калибр
    // 5    : Плоский овальный калибр
    // 6    : Шестиугольный калибр
    // 7    : Ромбический калибр
    // 8    : Ящичный калибр
    // 9    : Ребровой овальный калибр
    // 10   : Шестигранный калибр (правильный шестиугольник)
    // 11   : Ребровой калибр (в логике часто приводится к ящичному или гладкой бочке)
    // 12   : Кантованный квадрат

    /// <summary>
    /// Контекст вычислений
    /// </summary>
    public class CalculationContext
    {
        public double PI = 3.14159;
        public List<string> CalculationErrors = new List<string>();

        // Глобальные переменные
        public double M, W0, P0, L0, T0, TAU, LR, VK;
        public double K1, K2, K3, K4, K5, K6, K7, K8, K9;
        public double K10, K11, K12, K13, K14, K15, K16, K17, K18, K19, K20, TS;
        public int NPR;

        // Входные массивы (размер 41 для индексов 1..40)
        public int[] N = new int[41];
        public int[] SXEMA = new int[41];
        public double[] H0 = new double[41], B0 = new double[41], H1 = new double[41], B1 = new double[41], W = new double[41];
        public double[] S = new double[41], BVR = new double[41], BD = new double[41], R = new double[41], ROV = new double[41], R8 = new double[41];
        public double[] SUMX = new double[41], PSI = new double[41];
        public int[] Z = new int[41], SP = new int[41];
        public double[] TOP = new double[41], DB = new double[41], DSH = new double[41], MU = new double[41], FPOD = new double[41];
        public double[] LKL = new double[41], AKL = new double[41], IR = new double[41], ETA = new double[41], NZ = new double[41];
        public double[] NNOM = new double[41], PP = new double[41], NDVN = new double[41], NDVMIN = new double[41], NDVMAX = new double[41];
        public double[] PDOP = new double[41], MDOP = new double[41], C = new double[41];

        // Расчетные массивы
        public double[] V1 = new double[41], DK = new double[41], NR = new double[41], NN = new double[41], NMIN = new double[41], NMAX = new double[41];
        public double[] VMIN = new double[41], VMAX = new double[41], KSI = new double[41];
        public int[] KODP = new int[41];
        public double[] LP = new double[41], SIGSP = new double[41], SIGSZ = new double[41], SIGSSR = new double[41];
        public double[] TP = new double[41], TZ = new double[41], TM = new double[41];
        public double[] DTLP = new double[41], DTLZ = new double[41], DTDP = new double[41], DTDZ = new double[41], DTDSR = new double[41];
        public double[] DTP = new double[41], DTZ = new double[41], T1P = new double[41], T1Z = new double[41], TSR = new double[41], PSITR = new double[41];

        public double[] BK = new double[41], HVR = new double[41], HG = new double[41], PER = new double[41];
        public int[] KOD0 = new int[41], KOD1 = new int[41];
        public double[] DD = new double[41];

        public double[] A0 = new double[41], A1 = new double[41], AK = new double[41], AZ = new double[41], DEL0 = new double[41], DEL1 = new double[41];
        public double[] TANFI = new double[41], A = new double[41], KOBJ = new double[41], KVIT = new double[41], E = new double[41], DELV = new double[41], KUSH = new double[41];
        public double[] WR = new double[41], KVITR = new double[41];

        public double[] BETAR = new double[41], KBETAP = new double[41], KBETAZ = new double[41], BETASTP = new double[41], BETASTZ = new double[41];
        public double[] SIGSBP = new double[41], SIGSBZ = new double[41], B1RP = new double[41], B1RZ = new double[41];
        public double[] DELOP = new double[41], DELRP = new double[41], DELRZ = new double[41];

        public double[] DELH = new double[41], DELB = new double[41], UGZAX = new double[41], LOD = new double[41];
        public double[] ALFA = new double[41], ADOP = new double[41];

        public double[] FKON = new double[41], NSIG = new double[41], LH = new double[41], NVAL = new double[41];
        public double[] PSRP = new double[41], PSRZ = new double[41], P1P = new double[41], P1Z = new double[41];
        public double[] KPP = new double[41], KPZ = new double[41], MDP = new double[41], MDZ = new double[41];
        public double[] MTRP = new double[41], MTRZ = new double[41], MPRP = new double[41], MPRZ = new double[41], NPRP = new double[41];
        public double[] KMP = new double[41], KMZ = new double[41], NPRZ = new double[41], RP = new double[41], RZ = new double[41];
        public double[] MIP = new double[41], MIZ = new double[41], MISUM = new double[41], MISUMP = new double[41], MISUMZ = new double[41];
        public double[] KDVP = new double[41], KDVZ = new double[41], MDV = new double[41], JJ = new double[41], NDVR = new double[41];
        public double[] WWP = new double[41], WWZ = new double[41], PLECHO = new double[41];
        public double[] DELDOP = new double[41];

        public double[] B1SP = new double[41], H1SP = new double[41], B0SP = new double[41], H0SP = new double[41];
        public double[] ESP = new double[41], LSP = new double[41], KSISP = new double[41], SIGSSPP = new double[41], SIGSSPZ = new double[41];
        public double[] DELSP = new double[41], MUSP = new double[41], NSIGSP = new double[41], HSRSP = new double[41], LHSR = new double[41];
        public double[] PSRSPP = new double[41], PSRSPZ = new double[41], FKONSP = new double[41], P1SPP = new double[41], P1SPZ = new double[41];
        public double[] RSPP = new double[41], RSPZ = new double[41], KPSPP = new double[41], KPSPZ = new double[41];
        public double[] MTRSPP = new double[41], MTRSPZ = new double[41], MDSPP = new double[41], MDSPZ = new double[41];
        public double[] MPRSPP = new double[41], MPRSPZ = new double[41], KMSPP = new double[41], KMSPZ = new double[41];
        public double[] NPSPP = new double[41], NPSPZ = new double[41], WWSPP = new double[41], WWSPZ = new double[41];
        public double[] MISPP = new double[41], MISPZ = new double[41], MISUMSPP = new double[41], MISUMSPZ = new double[41];
        public double[] KDVSPP = new double[41], KDVSPZ = new double[41], NGSP = new double[41];
        public double[] SU = new double[41], SUP = new double[41], SUZ = new double[41], FKLP = new double[41], FKLZ = new double[41], FKL = new double[41];
        public double[] VRMAX = new double[41];

        // Бэкапы для вывода в UI
        public int[] KOD0D = new int[41];
        public int[] KOD1D = new int[41];

        // Усредненные массивы для вывода (DataFromProgramm)
        public double[] MDS = new double[41], MISUMS = new double[41], MTRS = new double[41], MPRS = new double[41];
        public double[] KMS = new double[41], WWS = new double[41], PSRS = new double[41], P1S = new double[41];
        public double[] RS = new double[41], KPS = new double[41], NPRS = new double[41], KDVS = new double[41];
        public double[] NDVR_ = new double[41]; // Важный массив для групповых приводов

        // Для графиков
        public double[] A0_ = new double[41], A0__ = new double[41];
        public double[] DEL1_ = new double[41], DEL1__ = new double[41], DELV_ = new double[41], DELV__ = new double[41];
        public double[] UGZAX_ = new double[41], UGZAX__ = new double[41];
        public double[] KMS_ = new double[41], KMS__ = new double[41], KDVS_ = new double[41], KDVS__ = new double[41];
        public double[] KPS_ = new double[41], KPS__ = new double[41];
        public double[] NR_ = new double[41], NR__ = new double[41], NR___ = new double[41], NR____ = new double[41];

        // ======================= ФУНКЦИИ ЯДРА =======================

        /// <summary>
        /// Расчет параметров "нулевого" (исходного) прохода. 
        /// Определяет базовые коэффициенты заполнения, площади и ширины для заготовки перед первой клетью.
        /// </summary>
        public int par_0_data()
        {
            switch (KOD0[1])
            {
                case 0:
                case 1:
                case 2:
                case 12:
                    DEL1[0] = 1; DEL0[1] = 1;
                    break;
                case 3:
                case 6:
                case 7:
                    DEL1[0] = 0.85; DEL0[1] = 0.85;
                    break;
                case 4:
                    DEL1[0] = 0.80; DEL0[1] = 0.80;
                    break;
                case 5:
                case 10:
                    DEL1[0] = 0.95; DEL0[1] = 0.95;
                    break;
                case 8:
                case 9:
                    TANFI[0] = 0.3; DEL1[0] = 0.90; DEL0[1] = 0.90;
                    break;
            }
            BK[0] = H0[1] / DEL0[1];
            B1[0] = BK[0];
            AK[0] = BK[0] / B0[1];
            W[0] = W0;
            if (KOD0[1] == 4) ROV[0] = B0[1] * (1 + AK[0] * AK[0]) / 4;
            if (KOD0[1] == 8)
            {
                BD[0] = BK[0] - TANFI[0] * B0[1];
                DELV[0] = (H0[1] - BD[0]) / (BK[0] - BD[0]);
            }
            return 0;
        }

        /// <summary>
        /// Глобальная нормализация и валидация исходных данных. 
        /// Проверяет физические ограничения (H0 > H1, W0 > W1), корректность схем прокатки, 
        /// наличие коэффициентов стали и генерирует массив критических ошибок.
        /// </summary>
        public bool Analis_data()
        {
            CalculationErrors.Clear();
            bool hasCriticalErrors = false;

            for (int i = 1; i <= NPR; i++)
            {
                switch (KOD1[i])
                {
                    case 0:
                    case 1:
                        KOD1[i] = 0;
                        if (KOD0[i] == 12 || KOD0[i] == 3 || KOD0[i] == 5 || KOD0[i] == 6 || KOD0[i] == 8 || KOD0[i] == 11 || KOD0[i] == 4 || KOD0[i] == 9)
                            KOD0[i] = 1;
                        if (!(KOD0[i] == 1 || KOD0[i] == 2)) { CalculationErrors.Add($"Клеть {i}: Неправильно задан подкат для гладкой бочки."); hasCriticalErrors = true; }
                        break;
                    case 2:
                        if (KOD0[i] == 6 || KOD0[i] == 9 || KOD0[i] == 7) KOD0[i] = 4;
                        if (KOD0[i] == 1) KOD0[i] = 5;
                        if (!(KOD0[i] == 4 || KOD0[i] == 5)) { CalculationErrors.Add($"Клеть {i}: Неправильно задан подкат для круглого калибра."); hasCriticalErrors = true; }
                        break;
                    case 3:
                        if (KOD0[i] == 9) KOD0[i] = 4;
                        if (KOD0[i] == 10) KOD0[i] = 6;
                        if (KOD0[i] == 3) KOD0[i] = 7;
                        if (KOD0[i] != 4 && KOD0[i] != 6 && KOD0[i] != 7) { CalculationErrors.Add($"Клеть {i}: Неправильно задан подкат для квадратного калибра."); hasCriticalErrors = true; }
                        break;
                    case 4:
                        if (KOD0[i] == 3 || KOD0[i] == 12 || KOD0[i] == 5 || KOD0[i] == 8) KOD0[i] = 1;
                        if (KOD0[i] != 1 && KOD0[i] != 2 && KOD0[i] != 9) { CalculationErrors.Add($"Клеть {i}: Неправильно задан подкат для овального калибра."); hasCriticalErrors = true; }
                        break;
                    case 5:
                        if (KOD0[i] == 2 || KOD0[i] == 3 || KOD0[i] == 8 || KOD0[i] == 12 || KOD0[i] == 11) KOD0[i] = 1;
                        if (KOD0[i] != 1) { CalculationErrors.Add($"Клеть {i}: Неправильно задан подкат для плоского овала."); hasCriticalErrors = true; }
                        break;
                    case 6:
                        if (KOD0[i] == 3 || KOD0[i] == 12 || KOD0[i] == 11 || KOD0[i] == 10) KOD0[i] = 1;
                        if (KOD0[i] != 1) { CalculationErrors.Add($"Клеть {i}: Неправильно задан подкат для шестиугольного калибра."); hasCriticalErrors = true; }
                        break;
                    case 7:
                        if (KOD0[i] == 1 || KOD0[i] == 2 || KOD0[i] == 12 || KOD0[i] == 8 || KOD0[i] == 10) KOD0[i] = 3;
                        if (KOD0[i] != 3 && KOD0[i] != 7) { CalculationErrors.Add($"Клеть {i}: Неправильно задан подкат для ромбического калибра."); hasCriticalErrors = true; }
                        break;
                    case 8:
                        if (KOD0[i] == 3 || KOD0[i] == 12 || KOD0[i] == 11) KOD0[i] = 1;
                        if (KOD0[i] != 1 && KOD0[i] != 8) { CalculationErrors.Add($"Клеть {i}: Неправильно задан подкат для ящичного калибра."); hasCriticalErrors = true; }
                        break;
                    case 9:
                        if (KOD0[i] == 6 || KOD0[i] == 7 || KOD0[i] == 5 || KOD0[i] == 1) KOD0[i] = 4;
                        if (KOD0[i] != 4) { CalculationErrors.Add($"Клеть {i}: Неправильно задан подкат для ребрового овала."); hasCriticalErrors = true; }
                        break;
                    case 10:
                        KOD1[i] = 10;
                        if (KOD0[i] == 8) KOD0[i] = 6;
                        if (KOD0[i] != 6) { CalculationErrors.Add($"Клеть {i}: Неправильно задан подкат для шестигранного калибра."); hasCriticalErrors = true; }
                        break;
                    case 11:
                        KOD1[i] = 0;
                        if (KOD0[i] == 12 || KOD0[i] == 8 || KOD0[i] == 5 || KOD0[i] == 4 || KOD0[i] == 6) KOD0[i] = 1;
                        if (KOD0[i] != 1) { CalculationErrors.Add($"Клеть {i}: Неправильно задан подкат для ребрового калибра."); hasCriticalErrors = true; }
                        break;
                }

                if (H0[i] < H1[i] && K17 == 0)
                {
                    CalculationErrors.Add($"Клеть {i}: Ошибка! Неправильно задана высота подката (H0 < H1).");
                    hasCriticalErrors = true;
                }

                if (i == 1 && K17 == 0 && K14 != 8)
                {
                    if (W0 < W[i]) { CalculationErrors.Add($"Клеть 1: Неправильно задана площадь сечения (W0 < W1)."); hasCriticalErrors = true; }
                }
                else if (i > 1 && W[i - 1] < W[i] && K17 == 0 && K14 != 8)
                {
                    CalculationErrors.Add($"Клеть {i}: Неправильно задана площадь сечения (W[{i - 1}] < W[{i}]).");
                    hasCriticalErrors = true;
                }

                if (B1[i] < B0[i] && K17 == 0) CalculationErrors.Add($"Клеть {i}: Неправильно задана ширина подката (B1 < B0).");
                if (B1[i] > BVR[i] && K17 == 0) CalculationErrors.Add($"Клеть {i}: ПЕРЕПОЛНЕНИЕ КАЛИБРА (B1 > BVR).");

                if (KOD1[i] == 4)
                {
                    double rr = (H1[i] - S[i]) * (1 + (BVR[i] / (H1[i] - S[i])) * (BVR[i] / (H1[i] - S[i]))) / 4;
                    if (rr + 0.001 < ROV[i] || rr > 0.001 + ROV[i])
                    {
                        ROV[i] = rr;
                        CalculationErrors.Add($"Клеть {i}: Несоответствие расчетного и заданного радиусов овала. Скорректировано.");
                    }
                }

                if (H1[i] <= 0.001 || B1[i] <= 0.001) { CalculationErrors.Add($"Клеть {i}: H1 и B1 должны быть больше нуля."); hasCriticalErrors = true; }
            }

            for (int i = 1; i <= NPR; i++)
            {
                if (KOD1[i] > 9) SXEMA[i] = KOD0[i] * 100 + KOD1[i];
                else SXEMA[i] = KOD0[i] * 10 + KOD1[i];
            }

            if (K12 == 1)
            {
                for (int i = 1; i <= NPR; i++)
                    if (NZ[i] < 1) { CalculationErrors.Add($"Клеть {i}: Задайте частоты вращения валков (NZ)."); hasCriticalErrors = true; }
            }

            if ((K11 == 1 || K11 == 3) && (Math.Abs(K1) < 0.01 || Math.Abs(K2) == 0 || Math.Abs(K3) == 0 || Math.Abs(K4) == 0))
            {
                CalculationErrors.Add("Критическая ошибка: Не введены коэффициенты K1-K4.");
                hasCriticalErrors = true;
            }

            if (K11 == 2 && (K5 == 0 || K6 == 0 || K7 == 0 || K8 == 0 || K9 == 0))
            {
                CalculationErrors.Add("Критическая ошибка: Не введены коэффициенты K5-K9.");
                hasCriticalErrors = true;
            }

            return hasCriticalErrors;
        }

        /// <summary>
        /// Главный оркестратор расчета по методу кафедры ОМД (вариационный принцип минимума полной мощности).
        /// Последовательно вызывает функции расчета геометрии, кинематики, температуры, уширения и энергетики.
        /// </summary>
        public void domain()
        {
            for (int i = 1; i <= NPR; i++) raskat(i);
            if (TS == 1 || TS == 2) skorost((int)TS, (int)K12); else skorost2();
            tempsig();
            ushirenie();
            ugoln();
            ustoichn();
            power_OMD();
            skorost_max();
            lastcorrection();
        }

        /// <summary>
        /// Главный оркестратор расчета по методу соответственной полосы (Врацкого-Головина).
        /// </summary>
        public void domainSP()
        {
            for (int i = 1; i <= NPR; i++) raskat(i);
            if (TS == 1 || TS == 2) skorost((int)TS, (int)K12); else skorost2();
            tempsig();
            sp_polosa();
        }

        /// <summary>
        /// Расчет параметров одного прохода (раската): геометрии калибра, формоизменения, 
        /// относительных параметров и абсолютных параметров очага деформации.
        /// </summary>
        public void raskat(int i)
        {
            razmer(SXEMA[i], i);
            if (K17 > 0) forma(i, SXEMA[i]);
            parameter(SXEMA[i], i);
            ochag(i);
            if (K14 == 9) WR[i] = omega(i);
        }

        /// <summary>
        /// Доопределение геометрических параметров калибров (глубина вреза, ширина по врезу, 
        /// геометрическая высота, периметр, катающий диаметр) в зависимости от кода формы калибра (KOD1).
        /// </summary>
        public int razmer(int sxema, int i)
        {
            HVR[i] = (H1[i] - S[i]) / 2;
            if (!(KOD1[i] == 0 || KOD1[i] == 1)) HG[i] = H1[i];
            double GAMMA;
            DD[i] = (DB[i] + S[i] - H1[i]);
            switch (KOD1[i])
            {
                case 0:
                case 1:
                    HVR[i] = 0; BK[i] = 0;
                    if (KOD0[i] == 2) BD[i] = B1[i] - H1[i];
                    PER[i] = 2 * (H1[i] + B1[i]);
                    DD[i] = DB[i];
                    break;
                case 2:
                    GAMMA = 8;
                    if (H1[i] < 100) GAMMA = 11.5;
                    if (H1[i] < 55) GAMMA = 15.5;
                    if (H1[i] < 45) GAMMA = 22;
                    if (H1[i] < 30) GAMMA = 26;
                    BK[i] = H1[i] / Math.Cos(GAMMA / 57.3);
                    PER[i] = PI * H1[i];
                    break;
                case 3:
                    HG[i] = H1[i] + 0.83 * R[i];
                    if (R[i] > 0.001) BK[i] = HG[i]; else BK[i] = BVR[i] + S[i];
                    PER[i] = 2.828 * BK[i];
                    break;
                case 4:
                    BK[i] = H1[i] * (Math.Sqrt((4 * ROV[i] / H1[i]) - 1));
                    PER[i] = 2 * Math.Sqrt(B1[i] * B1[i] + 4 * (H1[i] * H1[i]) / 3);
                    break;
                case 5:
                    BK[i] = BD[i] + H1[i];
                    if (BK[i] < BVR[i]) BK[i] = BVR[i];
                    PER[i] = PI * H1[i] + 2 * (B1[i] - H1[i]);
                    break;
                case 6:
                case 8:
                    if (KOD0[i] == 6)
                    {
                        PER[i] = 3 * H1[i];
                        BK[i] = H1[i] * (0.866 + 0.5 * (BVR[i] - 0.866 * H1[i]) / (0.5 * H1[i] - S[i]));
                    }
                    else
                    {
                        BK[i] = BD[i] + H1[i] * (BVR[i] - BD[i]) / (H1[i] - S[i]);
                        PER[i] = 2 * (BD[i] + H1[i]) / Math.Cos(Math.Atan((BVR[i] - BD[i]) / (H1[i] - S[i])));
                    }
                    break;
                case 7:
                    HG[i] = H1[i] + 2 * R[i] * (Math.Sqrt(1 + 1 / (Math.Tan(118 / (2 * 57.3)) * Math.Tan(118 / (2 * 57.3)))) - 1);
                    BK[i] = BVR[i] + (BVR[i] / (HG[i] - S[i])) * S[i];
                    PER[i] = 2 * Math.Sqrt(H1[i] * H1[i] + B1[i] * B1[i]);
                    break;
                case 9:
                    BK[i] = BVR[i] + 2 * ROV[i] * (1 - Math.Cos(Math.Asin(S[i] / (2 * ROV[i]))));
                    PER[i] = 2 * Math.Sqrt(H1[i] * H1[i] + 4 * B1[i] * B1[i] / 3);
                    break;
                case 10:
                    BK[i] = BVR[i] + S[i] * ((BVR[i] - H1[i] / 1.154) / (H1[i] / 2 - S[i]));
                    PER[i] = 3 * H1[i];
                    break;
                default: return -1;
            }
            return 0;
        }

        /// <summary>
        /// Расчет относительного обжатия (степени деформации) с учетом эмпирических поправок 
        /// для специфических схем прокатки (ромбические, фасонные переходы).
        /// </summary>
        public double stepdef(int i, int sxema)
        {
            E[i] = (H0[i] - H1[i]) / H0[i];
            if (sxema == 37 || sxema == 73 || sxema == 77 || sxema == 24 || sxema == 42 || sxema == 44 || sxema == 94 || sxema == 49 || sxema == 20)
                E[i] = 2 * E[i] / 3;
            if (sxema == 14) E[i] = 1 - H1[i] * (1.16 - 0.2 * DEL1[i]) / H0[i];
            if (sxema == 15) E[i] = E[i] * (1 + (0.031 * (KOBJ[i] / (AK[i] - 1) - 0.3) * (KOBJ[i] / (AK[i] - 1) - 0.3) - 0.01) * (KOBJ[i] * KOBJ[i]) - 0.027 * (KOBJ[i] / (AK[i] - 1) - 0.5) * (KOBJ[i] / (AK[i] - 1) - 0.5));
            if (sxema == 16) E[i] = 1 - (H1[i] / H0[i]) + 0.5 * (1 - (AK[i] - 1) / KOBJ[i]) * (1 - (AK[i] - 1) / KOBJ[i]);
            return E[i];
        }

        /// <summary>
        /// Расчет безразмерных относительных параметров прокатки: отношения осей, 
        /// коэффициенты заполнения, обжатия, вытяжки и приведенный диаметр.
        /// </summary>
        public int parameter(int sxema, int i)
        {
            A1[i] = B1[i] / H1[i];
            if (BK[i] > 0) AK[i] = BK[i] / HG[i]; else AK[i] = 1;
            A0[i] = H0[i] / B0[i];
            if (BK[i] == 0) DEL1[i] = 1; else DEL1[i] = B1[i] / BK[i];
            KOBJ[i] = H0[i] / H1[i];
            KUSH[i] = B1[i] / B0[i];
            A[i] = DD[i] / H1[i];
            if (DEL1[i] > 1) { DELDOP[i] = DEL1[i]; DEL1[i] = 1; }
            if (sxema == 16 || sxema == 18 || sxema == 88 || sxema == 15) AZ[i] = B0[i] / BD[i]; else AZ[i] = 0;
            if (sxema == 18 || sxema == 88 || sxema == 16)
            {
                TANFI[i] = (BK[i] - BD[i]) / H1[i];
                DELV[i] = (B1[i] - BD[i]) / (BK[i] - BD[i]);
            }
            else
            {
                if (sxema == 610) TANFI[i] = (BK[i] - 0.866 * H1[i]) / (0.5 * H1[i]); else TANFI[i] = 0;
                DELV[i] = 0;
            }
            if (Math.Abs(W[i]) < 0.1) W[i] = omega(i);
            if (i == 1) { KVIT[i] = W0 / W[i]; } else { KVIT[i] = W[i - 1] / W[i]; DEL0[i] = DEL1[i - 1]; }
            if (KVIT[i] < 1.0001 || KOBJ[i] < 1.0001) return -1;
            stepdef(i, sxema);
            return 0;
        }

        /// <summary>
        /// Расчет абсолютных параметров очага деформации: абсолютное обжатие, уширение, 
        /// длина дуги захвата и геометрический угол захвата.
        /// </summary>
        public void ochag(int i)
        {
            DELH[i] = H0[i] - H1[i];
            DELB[i] = B1[i] - B0[i];
            LOD[i] = Math.Sqrt(DELH[i] * DD[i] / 2);
            UGZAX[i] = Math.Asin(Math.Sqrt(DELH[i] / (2 * DD[i]))) * 2 * 180 / PI;
        }

        /// <summary>
        /// Расчет площади поперечного сечения полосы по эмпирическим формулам 
        /// в зависимости от кода формы калибра (KOD1) и степени заполнения.
        /// </summary>
        public double omega(int i)
        {
            double cc, aa, bb;
            switch (KOD1[i])
            {
                case 0:
                case 1:
                    WR[i] = A1[i];
                    if (KOD0[i] == 2) { WR[i] = WR[i] * (1 - 0.333 * (1 - Math.Sqrt(1 - 1 / (A1[i] * A1[i])))); WR[i] = WR[i] * H1[i] * H1[i]; }
                    else WR[i] = WR[i] * H1[i] * H1[i];
                    break;
                case 2:
                    WR[i] = 0.785 - 0.667 * (1 - DEL1[i]) * Math.Sqrt(1 - DEL1[i] * DEL1[i]);
                    WR[i] = WR[i] * H1[i] * H1[i];
                    break;
                case 3:
                    cc = (H1[i] + 0.83 * R[i]) / Math.Sqrt(2);
                    WR[i] = DEL1[i] * (2 - DEL1[i]) - 0.43 * (R[i] / cc) * (R[i] / cc);
                    WR[i] = WR[i] * cc * cc;
                    break;
                case 4:
                    WR[i] = 0.6 * (2.07 - DEL1[i]) * (A1[i] + 0.66 * DEL1[i] - 0.43);
                    WR[i] = WR[i] * H1[i] * H1[i];
                    break;
                case 5:
                    WR[i] = (AK[i] - 0.215) - 0.667 * (1 - DEL1[i]) * Math.Sqrt(1 - DEL1[i] * DEL1[i]);
                    WR[i] = WR[i] * H1[i] * H1[i];
                    break;
                case 6:
                    if (KOD0[i] == 6) WR[i] = 0.83 * H1[i] / 2 * H1[i] / 2;
                    else WR[i] = H1[i] * (BD[i] + (1 + (1 - DEL1[i]) / (1 - BD[i] / BK[i])) * (B1[i] - BD[i]) / 2) - 0.088 * R[i] * R[i];
                    break;
                case 7:
                    WR[i] = 0.5 * AK[i] * DEL1[i] * (2 - DEL1[i]) - 0.43 * (R[i] / H1[i]) * (R[i] / H1[i]);
                    WR[i] = WR[i] * H1[i] * H1[i];
                    break;
                case 8:
                    WR[i] = A1[i] - DELV[i] * DELV[i] * TANFI[i] / 2 - 0.55 * (R[i] / H1[i]) * (R[i] / H1[i]);
                    WR[i] = WR[i] * H1[i] * H1[i];
                    break;
                case 9:
                    aa = 1 + 1 / (AK[i] * AK[i]);
                    bb = 1 + 1 / AK[i];
                    cc = 1 / (AK[i] * AK[i]) - 1;
                    WR[i] = 0.15 * AK[i] * AK[i] * (aa * aa * (2.07 - DEL1[i]) * (1.66 * DEL1[i] - 0.43) - 0.833 * cc * bb * bb);
                    WR[i] = WR[i] * H1[i] * H1[i];
                    break;
                case 10:
                    WR[i] = 0.866 * (H1[i] / 1.154) * (H1[i] / 1.154);
                    break;
            }
            if (i == 1) KVITR[i] = W0 / WR[i]; else KVITR[i] = WR[i - 1] / WR[i];
            return WR[i];
        }

        /// <summary>
        /// Расчет скоростного режима (скорости полосы, частоты вращения валков, скорости деформации) 
        /// для непрерывных и последовательных станов.
        /// </summary>
        public void skorost(int TS_local, int K12_local)
        {
            int i = 1;
            while (i <= NPR)
            {
                NN[i] = NDVN[i] / IR[i];
                NMIN[i] = NDVMIN[i] / IR[i];
                NMAX[i] = NDVMAX[i] / IR[i];
                if (TS_local == 1)
                {
                    double V0 = VK / (W0 / W[NPR]);
                    if (i == 1) V1[i] = V0 * KVIT[i]; else V1[i] = V1[i - 1] * KVIT[i];
                    DK[i] = (DB[i] + S[i]) - W[i] / B1[i];
                    NR[i] = V1[i] / (PI * DK[i]) * 60000;
                    if (PP[i] < 0.5) NR[i] = NR[i - 1] * IR[i - 1] / IR[i];
                    if (K12_local == 1) { NR[i] = NZ[i]; V1[i] = PI * DK[i] * NR[i] / 60000; }
                }
                else if (TS_local == 2)
                {
                    NR[i] = NZ[i];
                    DK[i] = (DB[i] + S[i]) - W[i] / B1[i];
                    V1[i] = PI * DK[i] * NR[i] / 60000;
                }
                VMAX[i] = PI * DK[i] * NMAX[i] * (1 - LR) / 60000;
                VMIN[i] = PI * DK[i] * NMIN[i] * (1 + LR) / 60000;
                KSI[i] = 0.105 * NR[i] * Math.Sqrt(E[i] * DK[i] / (2 * H0[i]));
                i++;
            }
        }

        /// <summary>
        /// Расчет скоростного режима для полунепрерывных станов с петлевыми группами 
        /// (расчет кинематики идет от чистовой клети к черновой).
        /// </summary>
        public void skorost2()
        {
            int i = 1;
            while (i <= NPR)
            {
                NN[i] = NDVN[i] / IR[i];
                NMIN[i] = NDVMIN[i] / IR[i];
                NMAX[i] = NDVMAX[i] / IR[i];
                DK[i] = (DB[i] + S[i]) - W[i] / B1[i];
                i++;
            }
            i = NPR;
            while (i >= 1)
            {
                if (i == NPR) { V1[i] = VK; NR[i] = 60000 * V1[i] / (PI * DK[i]); }
                else if (PP[i + 1] < 0.9999 && PP[i] > 0.1) { NR[i] = NR[i + 1]; V1[i] = PI * DK[i] * NR[i] / 60000; }
                else { V1[i] = V1[i + 1] / KVIT[i + 1]; NR[i] = 60000 * V1[i] / (PI * DK[i]); if (PP[i + 1] < 0.5) NR[i] = NR[i + 1] * IR[i + 1] / IR[i]; }
                if (K12 == 1) { NR[i] = NZ[i]; V1[i] = PI * DK[i] * NR[i] / 60000; }
                VMAX[i] = PI * DK[i] * NMAX[i] * (1 - LR) / 60000;
                VMIN[i] = PI * DK[i] * NMIN[i] * (1 + LR) / 60000;
                KSI[i] = 0.105 * NR[i] * Math.Sqrt(E[i] * DK[i] / (2 * H0[i]));
                i--;
            }
        }

        /// <summary>
        /// Расчет температуры раската после временного интервала с учетом охлаждения лучеиспусканием 
        /// (закон Стефана-Больцмана) и экзотермического разогрева от работы пластической деформации.
        /// </summary>
        public double temp(double PP_param, double TT, double WW, double DTD, double T1)
        {
            return (1000 / (Math.Exp(0.333 * Math.Log((0.0254 * PP_param * TT / WW) + Math.Pow(1000 / (T1 + DTD + 273), 3))))) - 273;
        }

        /// <summary>
        /// Расчет сопротивления деформации по методу Зюзина-Третьякова (термомеханические коэффициенты K1-K4).
        /// </summary>
        public double sig1(double k1, double k2, double k3, double k4, double e, double ksi, double t1) => k1 * Math.Exp(k2 * Math.Log(e)) * Math.Exp(k3 * Math.Log(ksi)) / Math.Exp(k4 * t1);

        /// <summary>
        /// Расчет сопротивления деформации по методу Андреюка-Тюленева (5 коэффициентов K5-K9).
        /// </summary>
        public double sig2(double k5, double k6, double k7, double k8, double k9, double e, double ksi, double t1) => k5 * k6 * Math.Exp(k7 * Math.Log(ksi)) * Math.Exp(k8 * Math.Log(e)) * Math.Exp(k9 * Math.Log(t1 / 1000));

        /// <summary>
        /// Расчет сопротивления деформации по методу кафедры ОМД с учетом динамического разупрочнения.
        /// </summary>
        public double sig3(double k1, double k2, double k3, double k4, double k5, double e, double ksi, double t1) => k1 * Math.Exp(k2 * Math.Log(e)) * Math.Exp(k3 * Math.Log(ksi)) * Math.Exp(k5 * e) / Math.Exp(k4 * t1);

        /// <summary>
        /// Расчет температурного баланса и сопротивления деформации для переднего и заднего концов 
        /// полосы во всех проходах. Также определяет показатель контактного трения.
        /// </summary>
        public void tempsig()
        {
            for (int i = 1; i <= NPR; i++)
            {
                if (i == 1) { LP[i] = L0 * KVIT[i]; TM[i] = LP[i] / V1[i]; TP[i] = TAU; TZ[i] = TP[i] + TM[i]; }
                else { LP[i] = LP[i - 1] * KVIT[i]; TM[i] = LP[i] / V1[i]; TP[i] = LKL[i - 1] / V1[i - 1]; TZ[i] = TP[i] + (TM[i] - TM[i - 1]); }

                if (K13 == 9) { T1P[i] = TOP[i]; T1Z[i] = TOP[i]; }
                else
                {
                    if (i == 1)
                    {
                        T1P[i] = temp(P0, TP[i], W0, 0, T0); T1Z[i] = temp(P0, TZ[i], W0, 0, T0);
                        DTP[i] = T0 - T1P[i]; DTZ[i] = T0 - T1Z[i]; DTLP[i] = DTP[i]; DTLZ[i] = DTZ[i];
                    }
                    else
                    {
                        DTDP[i - 1] = 0.183 * SIGSP[i - 1] * Math.Log(KVIT[i - 1]);
                        DTDZ[i - 1] = 0.183 * SIGSZ[i - 1] * Math.Log(KVIT[i - 1]);
                        T1P[i] = temp(PER[i - 1], TP[i], W[i - 1], DTDP[i - 1], T1P[i - 1]);
                        T1Z[i] = temp(PER[i - 1], TZ[i], W[i - 1], DTDZ[i - 1], T1Z[i - 1]);
                        DTP[i] = T1P[i - 1] - T1P[i]; DTZ[i] = T1Z[i - 1] - T1Z[i];
                        DTLP[i] = DTP[i] - DTDP[i - 1]; DTLZ[i] = DTZ[i] - DTDZ[i - 1];
                    }
                }
                TSR[i] = T1P[i];
                if (SXEMA[i] == 37 || SXEMA[i] == 73 || SXEMA[i] == 77)
                {
                    PSITR[i] = 0.5; if (TSR[i] < 1100) PSITR[i] = 0.6; if (TSR[i] < 1000) PSITR[i] = 0.75;
                }
                else
                {
                    PSITR[i] = 0.6; if (TSR[i] < 1200) PSITR[i] = 0.7; if (TSR[i] < 1100) PSITR[i] = 0.8; if (TSR[i] < 1000) PSITR[i] = 0.9;
                    if (KOD1[i] == 8 || KOD1[i] == 1 || KOD1[i] == 0) PSITR[i] = PSITR[i] - 0.1;
                }
                if (TSR[i] < 900) PSITR[i] = 1;

                if (K11 == 1) { SIGSP[i] = sig1(K1, K2, K3, K4, E[i], KSI[i] > 120 ? 120 : KSI[i], T1P[i]); SIGSZ[i] = sig1(K1, K2, K3, K4, E[i], KSI[i] > 120 ? 120 : KSI[i], T1Z[i]); }
                if (K11 == 2) { SIGSP[i] = sig2(K5, K6, K7, K8, K9, E[i], KSI[i] > 120 ? 120 : KSI[i], T1P[i]); SIGSZ[i] = sig2(K5, K6, K7, K8, K9, E[i], KSI[i] > 120 ? 120 : KSI[i], T1Z[i]); }
                if (K11 == 3) { SIGSP[i] = sig3(K1, K2, K3, K4, K5, E[i], KSI[i] > 120 ? 120 : KSI[i], T1P[i]); SIGSZ[i] = sig3(K1, K2, K3, K4, K5, E[i], KSI[i] > 120 ? 120 : KSI[i], T1Z[i]); }

                SIGSP[i] *= 9.807; SIGSZ[i] *= 9.807;
            }
        }

        /// <summary>
        /// Базовая формула расчета коэффициента уширения на основе вариационного принципа 
        /// минимума полной мощности (зависит от 7 безразмерных параметров и эмпирических констант C0-C7).
        /// </summary>
        public double spred(double C0, double C1, double C2, double C3, double C4, double C5, double C6, double C7, double KOBJ, double A, double A0, double AK, double DEL0, double PSITR, double TANFI)
        {
            double AA = Math.Exp(C1 * Math.Log(KOBJ - 1));
            double BB = Math.Exp(C2 * Math.Log(A));
            double CC = Math.Exp(C3 * Math.Log(A0));
            double DD = AK < 0.001 ? 1 : Math.Exp(C4 * Math.Log(AK));
            double EE = Math.Exp(C5 * Math.Log(DEL0));
            double FF = Math.Exp(C6 * Math.Log(PSITR));
            double GG = ((int)(TANFI * 1000) != 0) ? Math.Exp(C7 * Math.Log(TANFI)) : 1;
            return 1 + C0 * AA * BB * CC * DD * EE * FF * GG;
        }

        /// <summary>
        /// Расчет фактического уширения и ширины полосы с учетом поправки на марку стали 
        /// (отношение сопротивления деформации прокатываемой стали к базовой Ст3).
        /// </summary>
        public void ushirenie()
        {
            for (int i = 1; i <= NPR; i++)
            {
                switch (KOD1[i])
                {
                    case 0:
                    case 1:
                        if (KOD0[i] == 2) BETAR[i] = spred(0.179, 1.357, 0.291, 0, 0, 0, 0.511, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        if (KOD0[i] == 1) BETAR[i] = spred(0.0714, 0.862, 0.555, 0.763, 0, 0, 0.455, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        break;
                    case 2:
                        if (KOD0[i] == 4) BETAR[i] = spred(0.386, 1.163, 0.402, -2.171, 0, -1.324, 0.616, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        if (KOD0[i] == 5) BETAR[i] = spred(0.693, 1.286, 0.368, -1.052, 0, -2.231, 0.629, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        break;
                    case 3:
                        if (KOD0[i] == 4) BETAR[i] = spred(2.242, 1.151, 0.352, -2.234, 0, -1.647, 1.137, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        if (KOD0[i] == 6) BETAR[i] = spred(0.360, 0.658, 0.202, -0.467, 0, -3.316, 0.494, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        if (KOD0[i] == 7) BETAR[i] = spred(0.972, 2.01, 0.665, -2.458, 0, -1.3, 0.7, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        break;
                    case 4:
                        if (KOD0[i] == 1 || KOD0[i] == 8) BETAR[i] = spred(0.377, 0.507, 0.316, 0, -0.405, 0, 1.136, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        if (KOD0[i] == 2) BETAR[i] = spred(0.227, 1.563, 0.591, 0, -0.852, 0, 0.587, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        if (KOD0[i] == 4) BETAR[i] = spred(0.405, 1.163, 0.403, -2.171, -0.789, -1.324, 0.616, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        if (KOD0[i] == 9) BETAR[i] = spred(1.623, 2.272, 0.761, -0.582, -3.064, 0, 0.486, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        break;
                    case 5:
                        if (KOD0[i] == 1) BETAR[i] = spred(0.134, 0.717, 0.474, 0, -0.507, 0, 0.357, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        break;
                    case 6:
                        if (KOD0[i] == 1) BETAR[i] = spred(2.075, 1.848, 0.815, 0, -3.453, 0, 0.659, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        break;
                    case 7:
                        if (KOD0[i] == 3) BETAR[i] = spred(3.09, 2.07, 0.5, 0, -4.85, -4.865, 1.543, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        if (KOD0[i] == 7) BETAR[i] = spred(0.506, 1.876, 0.895, -2.22, -2.22, -2.73, 0.587, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        break;
                    case 8:
                        if (KOD0[i] == 8 || KOD0[i] == 1) BETAR[i] = spred(0.0714, 0.862, 0.746, 0.763, 0, 0, 0.16, 0.362, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        break;
                    case 9:
                        if (KOD0[i] == 4) BETAR[i] = spred(0.575, 1.163, 0.402, -2.171, -4.265, -1.324, 0.616, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        break;
                    case 10:
                        if (KOD0[i] == 6) BETAR[i] = spred(0.3, 1.203, 0.368, -0.852, 0, -3.45, 0.629, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                        break;
                }

                if (K11 == 1 || K11 == 3)
                {
                    SIGSBP[i] = 9.807 * sig1(130, 0.252, 0.143, 0.0025, E[i], KSI[i] > 120 ? 120 : KSI[i], T1P[i]);
                    SIGSBZ[i] = 9.807 * sig1(130, 0.252, 0.143, 0.0025, E[i], KSI[i] > 120 ? 120 : KSI[i], T1Z[i]);
                }
                if (K11 == 2)
                {
                    SIGSBP[i] = 9.807 * sig2(1.41, 9.07, 0.124, 0.167, -2.54, E[i], KSI[i] > 120 ? 120 : KSI[i], T1P[i]);
                    SIGSBZ[i] = 9.807 * sig2(1.41, 9.07, 0.124, 0.167, -2.54, E[i], KSI[i] > 120 ? 120 : KSI[i], T1Z[i]);
                }
                KBETAP[i] = (SIGSP[i] / SIGSBP[i]) > 1.001 ? 1 + 0.6 * Math.Exp(0.544 * Math.Log((SIGSP[i] / SIGSBP[i]) - 1)) : 1;
                KBETAZ[i] = (SIGSZ[i] / SIGSBZ[i]) > 1.001 ? 1 + 0.6 * Math.Exp(0.544 * Math.Log((SIGSZ[i] / SIGSBZ[i]) - 1)) : 1;
                BETASTP[i] = 1 + (BETAR[i] - 1) * KBETAP[i];
                BETASTZ[i] = 1 + (BETAR[i] - 1) * KBETAZ[i];
                B1RP[i] = B0[i] * BETASTP[i];
                B1RZ[i] = B0[i] * BETASTZ[i];
                DELOP[i] = B1[i] / BVR[i];
                DELRP[i] = B1RP[i] / BVR[i];
                DELRZ[i] = B1RZ[i] / BVR[i];
            }
        }

        /// <summary>
        /// Вспомогательная функция для вычисления допустимого угла захвата по многофакторной регрессионной зависимости.
        /// </summary>
        public double angle(double C0, double C1, double C2, double C3, double C4, double C5, double C6, double V1, double MU, double M, double T1P, double PAR)
            => C6 / (C0 + C1 * V1 * V1 - C2 * MU + C3 * M + C4 * (T1P / 1000) + C5 * PAR);

        /// <summary>
        /// Расчет допустимых углов захвата для всех проходов в зависимости от схемы прокатки (SXEMA).
        /// </summary>
        public void ugoln()
        {
            for (int i = 1; i <= NPR; i++)
            {
                if (SXEMA[i] == 18 || SXEMA[i] == 88) ALFA[i] = angle(5.99, 0.266, 1.16, 0.42, 0.39, -1.9, 1.2, V1[i], MU[i], M, T1P[i], AZ[i]);
                if (SXEMA[i] == 14) ALFA[i] = angle(19.1, 0.00432, 1.03, 2.67, -13.7, -0.128, 1.3, V1[i], MU[i], M, T1P[i], ROV[i] / H1[i]);
                if (SXEMA[i] == 43) ALFA[i] = angle(16, 0.00303, 0.377, 2.7, -6.76, -7.65, 1.25, V1[i], MU[i], M, T1P[i], DEL0[i]);
                if (SXEMA[i] == 16) ALFA[i] = angle(10.3, 0.00375, 0.653, 0.071, -2.22, -1.78, 1.23, V1[i], MU[i], M, T1P[i], AZ[i]);
                if (SXEMA[i] == 63 || SXEMA[i] == 610) ALFA[i] = angle(13.7, 0.0092, 0.77, 0.23, -3.56, -5.1, 1.17, V1[i], MU[i], M, T1P[i], DEL0[i]);
                if (SXEMA[i] == 73 || SXEMA[i] == 37 || SXEMA[i] == 77) ALFA[i] = angle(8.82, 0.027, 0.406, 0.565, -3.23, -1.65, 1.25, V1[i], MU[i], M, T1P[i], DEL0[i]);
                if (SXEMA[i] == 42 || SXEMA[i] == 44) ALFA[i] = angle(27.74, 0.0023, 0.44, 2.15, -19.8, -3.98, 1.25, V1[i], MU[i], M, T1P[i], DEL0[i]);
                if (SXEMA[i] == 49) ALFA[i] = angle(5.56, 0.00328, 0.44, 0.0265, -0.759, -0.155, 1.12, V1[i], MU[i], M, T1P[i], DEL0[i]);
                if (SXEMA[i] == 94 || SXEMA[i] == 24) ALFA[i] = angle(23.54, 0.00265, 0.44, 0.374, -12.1, -5.22, 1.13, V1[i], MU[i], M, T1P[i], DEL0[i]);
                if (SXEMA[i] == 52) ALFA[i] = angle(9.23, 0.00284, 0.44, 0.644, 0.429, -6.32, 1.15, V1[i], MU[i], M, T1P[i], DEL0[i]);
                if (SXEMA[i] == 15) ALFA[i] = angle(7.4, 0.0024, 1.02, 0.49, -1.1, 0.18, 1.26, V1[i], MU[i], M, T1P[i], AZ[i]);
                ALFA[i] *= 100;
                if (SXEMA[i] == 10 || SXEMA[i] == 20 || SXEMA[i] == 11)
                {
                    double AA = MU[i] > 0.99 ? 1 : 0.8;
                    if (MU[i] > 1.2) AA = 1.2;
                    double BB = M > 1.3 ? 0.85 : 1;
                    double CC = V1[i] <= 2.05 ? 1 : 0.4 + 0.6 * Math.Exp(-0.2 * (V1[i] - 2));
                    ALFA[i] = Math.Atan(AA * BB * CC * (1.05 - 0.0005 * T1P[i])) * 180 / PI;
                }
            }
        }

        /// <summary>
        /// Вспомогательная функция для вычисления допустимого отношения осей по регрессионной зависимости.
        /// </summary>
        public double otos(double C0, double C1, double C2, double C3, double C4, double C5, double C6, double DEL0, double V1, double KOBJ, double PAR1, double PAR2)
            => (C0 - (C1 / (DEL0 * DEL0)) - C2 * V1 + (C3 / KOBJ) + C4 * PAR1 + C5 * PAR2) * C6;

        /// <summary>
        /// Расчет допустимого отношения осей подката (условие устойчивости полосы против сваливания в калибре).
        /// </summary>
        public void ustoichn()
        {
            for (int i = 1; i <= NPR; i++)
            {
                ADOP[i] = 1.01;
                if (SXEMA[i] == 18 || SXEMA[i] == 88)
                {
                    if (i == 1) ADOP[i] = otos(2, -0.022, 0.01, 0.13, -1.38, 0.21, 1.18, 0.8, V1[i], 1 / AZ[i], 0.4, TANFI[i]);
                    else if (KOD0[i] == 8) ADOP[i] = otos(2, -0.022, 0.01, 0.13, -1.38, 0.21, 1.18, DELV[i - 1], V1[i], 1 / AZ[i], TANFI[i - 1], TANFI[i]);
                    else ADOP[i] = otos(2, -0.022, 0.01, 0.13, -1.38, 0.21, 1.18, 0.8, V1[i], 1 / AZ[i], 0.4, TANFI[i]);
                }
                if (SXEMA[i] == 43) ADOP[i] = otos(4.23, 2.071, 0.012, 4.532, 0.0228, -2.42, 1.1, DEL0[i], V1[i], KOBJ[i], ROV[i - 1] / B0[i], R[i] / HG[i]);
                if (SXEMA[i] == 63 || SXEMA[i] == 610) ADOP[i] = otos(6.179, 1.619, 0.0335, 0.65, -0.567, -2.993, 1.2, DEL0[i], V1[i], KOBJ[i], B0[i] / BK[i], R[i] / HG[i]);
                if (SXEMA[i] == 73) ADOP[i] = otos(1.3, 0.338, 0.0107, 0.496, 1.134, -0.706, 1.2, DEL0[i], V1[i], KOBJ[i], B0[i] / BK[i], R[i] / HG[i]);
                if (SXEMA[i] == 77) ADOP[i] = otos(1.3, 0.338, 0.0107, 0.496, 1.134, -0.706, 1.1, DEL0[i], V1[i], KOBJ[i], B0[i] / BK[i], R[i] / HG[i]);
                if (SXEMA[i] == 42 || SXEMA[i] == 44) ADOP[i] = otos(1.8, 0.618, 0.005, 0.449, 0.812, 0, 1.19, DEL0[i], V1[i], KOBJ[i], ROV[i - 1] / B0[i], 0);
                if (SXEMA[i] == 49) ADOP[i] = otos(3.448, 0.725, 0.00282, 0.0588, 0.108, 0, 1.15, DEL0[i], V1[i], KOBJ[i], ROV[i - 1] / B0[i], 0);
                if (SXEMA[i] == 52) ADOP[i] = otos(2.38, 0.972, 0.00201, 1.819, 0, 0, 1.12, DEL0[i], V1[i], KOBJ[i], 0, 0);
                if (SXEMA[i] == 10 || SXEMA[i] == 11) ADOP[i] = 2;
            }
        }

        /// <summary>
        /// Расчет площади контактной поверхности между полосой и валками для различных схем прокатки.
        /// </summary>
        public double kontakt(int i)
        {
            FKON[i] = 0.5 * H1[i] * (B1[i] + B0[i]) * Math.Sqrt(0.5 * A[i] * (KOBJ[i] - 1));
            if (SXEMA[i] == 14)
            {
                double aa = KOBJ[i] - 1;
                double bb = 0.71 * DEL1[i] + 0.29;
                double cc = 0.375 * AK[i] + 0.845;
                double ee = bb * (0.28 * aa * aa + cc * aa + 0.09 * AK[i] + 0.213);
                FKON[i] = H1[i] * H1[i] * ee * (Math.Sqrt((A[i] + 1) / KOBJ[i] - 0.75));
            }
            if (SXEMA[i] == 43)
            {
                double aa = (AK[i - 1] - 0.2) * (AK[i - 1] - 0.2);
                double bb = Math.Sqrt((KOBJ[i] / DEL0[i]) + 0.4);
                double cc = (DEL0[i] - 0.1) * (DEL0[i] - 0.1);
                double ee = (0.23 + (1.86 + 6.7 / aa) * (bb - 1.2)) * (0.41 - 0.037 / cc) * (0.8 * DEL1[i] + 0.36);
                FKON[i] = H1[i] * H1[i] * ee * Math.Sqrt(A[i]);
            }
            if (SXEMA[i] == 77 || SXEMA[i] == 37 || SXEMA[i] == 73)
            {
                double aa = (AK[i - 1] * AK[i] - 1);
                double bb = KOBJ[i] - 1;
                double cc = KOBJ[i] / DEL0[i];
                double ee = (0.707 * AK[i] * ((0.6 * cc + 0.4 - KOBJ[i]) * Math.Sqrt(bb) + 2 * (Math.Sqrt(bb * bb * bb)) / 3) + 0.283 * AK[i] * (DEL1[i] * AK[i] * AK[i - 1] - 1) * Math.Sqrt(bb)) / aa;
                FKON[i] = H1[i] * H1[i] * ee * Math.Sqrt(A[i] - 0.25);
            }
            if (SXEMA[i] == 44 || SXEMA[i] == 42 || SXEMA[i] == 24 || SXEMA[i] == 49 || SXEMA[i] == 94)
            {
                double aa = A1[i] / Math.Sqrt(2);
                double bb = 1.62 - DEL0[i]; double ee;
                if (SXEMA[i] == 94 || SXEMA[i] == 24) ee = aa * bb * (1 + 0.4 / A1[i]); else ee = aa * bb * (1 - 0.1 * AK[i]);
                FKON[i] = H1[i] * H1[i] * ee * Math.Sqrt(A[i] * (KOBJ[i] - 1));
            }
            if (SXEMA[i] == 20) FKON[i] = FKON[i] * 0.785;
            return FKON[i];
        }

        /// <summary>
        /// Расчет коэффициента напряженного состояния (n_sigma) в очаге деформации 
        /// в зависимости от фактора формы и комбинации "подкат-калибр".
        /// </summary>
        public double nsigma(int i)
        {
            double aa, bb;
            LH[i] = (Math.Sqrt(2 * A[i] * (KOBJ[i] - 1))) / (KOBJ[i] + 1);
            switch (KOD1[i])
            {
                case 0:
                case 1:
                    NSIG[i] = (LH[i] - 4.23 + 18.55 / (LH[i] + 3)) * (1.108 - 0.102 * Math.Sqrt(A0[i])) * (0.68 + 0.32 * PSITR[i]);
                    if (KOD0[i] == 2) NSIG[i] = (LH[i] - 4.55 + 17.1 / (LH[i] + 2)) * (0.64 + 0.36 * PSITR[i]);
                    break;
                case 8:
                    NSIG[i] = (LH[i] - 5.55 + 37 / (LH[i] + 5)) * (0.0488 * A[i] + 0.534 * A[i] / (A[i] - 1)) * (0.745 + 0.051 / (TANFI[i] + 0.1)) * (1.108 - 0.102 * Math.Sqrt(A0[i])) * (1.225 - 0.18 / PSITR[i]);
                    break;
                case 9:
                    NSIG[i] = (LH[i] - 3.17 + 16.6 / (LH[i] + 3)) * (1.15 - 0.075 * A0[i]) * (0.88 + 0.16 * PSITR[i]);
                    break;
                case 5:
                    NSIG[i] = (LH[i] - 8 + 44.38 / (LH[i] + 4)) * (1.747 - 22.27 / (A[i] + 20)) * (1.08 - 0.18 / AK[i]) * (1.566 - 0.737 / (PSITR[i] + 0.5));
                    break;
                case 2:
                    NSIG[i] = (LH[i] - 3.7 + 18 / (LH[i] + 3)) * (1.15 - 0.075 * A0[i]) * (0.88 + 0.16 * PSITR[i]);
                    if (KOD0[i] == 5) NSIG[i] = (2 * LH[i] - 10 + 30.5 / (LH[i] + 2)) * (0.875 + 0.0694 * A0[i]) * (1.322 - 1.8 / Math.Sqrt(A[i] + 2)) * (0.7 + 0.375 * PSITR[i]);
                    break;
                case 4:
                    aa = (AK[i] * AK[i] + 1) / (AK[i] * AK[i]);
                    bb = (LH[i] - 6.14 + 40.5 / (LH[i] + 5));
                    if (KOD0[i] == 2 || KOD0[i] == 9) NSIG[i] = 0.9 * bb * aa * (0.63 + 0.37 * PSITR[i]);
                    if (KOD0[i] == 4) NSIG[i] = 0.92 * bb * aa * (0.6 + 0.4 * PSITR[i]);
                    if (KOD0[i] == 1) NSIG[i] = (LH[i] - 8.9 + 83 / (LH[i] + 8)) * (0.8 + 0.8 / AK[i]) * (0.61 + 0.39 * PSITR[i]);
                    break;
                case 7:
                    NSIG[i] = (LH[i] - 4.96 + 16.8 / (LH[i] + 2)) * (0.815 + 0.087 * A[i]) * (0.1 + DEL0[i]) * (0.885 + 0.192 * PSITR[i]);
                    if (KOD0[i] == 3) NSIG[i] = (LH[i] - 5.44 + 17.9 / (LH[i] + 2)) * ((A[i] + 2) / (0.45 * A[i] + 4.75)) * (1.1 * DEL0[i] - 0.045) * (0.829 + 0.285 * PSITR[i]);
                    break;
                case 3:
                    NSIG[i] = (LH[i] - 2.28 + 10 / (LH[i] + 2)) * (0.178 + 0.902 * DEL0[i]) * (0.8 + 0.25 * PSITR[i]);
                    if (KOD0[i] == 6) NSIG[i] = ((LH[i] + 1.86) - (LH[i] / (LH[i] + 1)) * (3.52 - 2.4 * DEL0[i] / A0[i])) * (0.65 + 0.35 * PSITR[i]);
                    if (KOD0[i] == 7) NSIG[i] = (LH[i] - 6.92 + 39.2 / (LH[i] + 4)) * ((A[i] + 2) / (0.693 * A[i] + 3.54)) * (1.05 * DEL0[i] + 0.055) * (0.829 + 0.285 * PSITR[i]);
                    break;
                case 6:
                    NSIG[i] = (LH[i] - 7.76 + 42.8 / (LH[i] + 4)) * (0.172 + 0.185 * Math.Sqrt(A[i] + 10)) * (1.088 - 0.105 / (AK[i] - 1)) * (2.42 - 2.56 / (PSITR[i] + 1));
                    if (KOD0[i] == 6) NSIG[i] = ((LH[i] + 1.86) - (LH[i] / (LH[i] + 1)) * (3.52 - 2.4 * DEL0[i] / A0[i])) * (0.65 + 0.35 * PSITR[i]);
                    break;
                case 10:
                    if (KOD0[i] == 6) NSIG[i] = ((LH[i] + 1.86) - (LH[i] / (LH[i] + 1)) * (3.52 - 2.4 * DEL0[i] / A0[i])) * (0.65 + 0.35 * PSITR[i]);
                    break;
            }
            return NSIG[i];
        }

        /// <summary>
        /// Расчет коэффициента мощности (плеча крутящего момента) в зависимости от формы калибра и подката.
        /// </summary>
        public double nvalkov(int i)
        {
            double aa, bb, cc;
            switch (KOD1[i])
            {
                case 0:
                case 1:
                    aa = KOBJ[i] - 1; bb = 0.05 + 4.8 / A[i]; cc = (55 / A[i]) - 1.1;
                    NVAL[i] = bb * (Math.Exp(0.68 * aa) - 1) * (0.68 + 0.32 * PSITR[i]) / A0[i];
                    if (KOD0[i] == 2) NVAL[i] = aa * aa * ((100 / A[i]) - 1 - aa * cc) * (0.069 + 0.039 * PSITR[i]);
                    break;
                case 8:
                    aa = KOBJ[i] - 1; bb = 0.0105 - 0.0012 * A0[i] * A0[i]; cc = (A0[i] + 0.1) * (A0[i] + 0.1);
                    NVAL[i] = KOBJ[i] * aa * (0.25 + (bb / TANFI[i]) - 0.024 * A0[i] * A0[i] + 0.254 * (1.02 - 0.2 * TANFI[i]) / cc) * (0.315 + 3.425 / (A[i] - 1)) * (1.225 - 0.18 / PSITR[i]);
                    break;
                case 4:
                    aa = KVIT[i];
                    NVAL[i] = aa * (aa - 1) * (9 / (A[i] + 10)) * (1.36 - 0.36 / PSITR[i]);
                    if (KOD0[i] == 2) NVAL[i] = (aa * aa - 1) * (0.094 + 3.66 / (A[i] + 5)) * (1.31 - 0.31 / PSITR[i]);
                    if (KOD0[i] == 4) NVAL[i] = (aa * aa - 1) * (0.1 + 2.7 / (A[i] + 5)) * (1.36 - 0.27 / PSITR[i]);
                    if (KOD0[i] == 9) NVAL[i] = (aa * aa - 1) * (0.013 + 4.4 / (A[i] + 5)) * (1.31 - 0.31 / PSITR[i]);
                    break;
                case 2:
                    aa = KVIT[i];
                    NVAL[i] = aa * (aa - 1) * (0.115 + 2.3 / (A[i] + 2)) * (1.36 - 0.27 / PSITR[i]);
                    if (KOD0[i] == 5) NVAL[i] = aa * (aa - 1) * (0.078 + 2.6 / (A[i] + 2)) * (1.36 - 0.27 / PSITR[i]);
                    break;
                case 9:
                    aa = KVIT[i];
                    NVAL[i] = aa * (aa - 1) * (0.09 + 1.84 / (A[i] + 2)) * (1.36 - 0.27 / PSITR[i]);
                    break;
                case 5:
                    aa = KVIT[i];
                    NVAL[i] = aa * (aa - 1) * (0.15 + 4.3 / (A[i] + 2)) * (1.46 - 0.69 / (PSITR[i] + 0.5));
                    break;
                case 3:
                    aa = KVIT[i];
                    NVAL[i] = aa * aa * (aa - 1) * (0.01 + 2.3 / (A[i] + 5)) * (1.2 - 0.16 / PSITR[i]);
                    if (KOD0[i] == 6) NVAL[i] = aa * aa * (aa - 1) * (0.045 + 1.2 / (A[i] + 2)) * (1.74 - 1.12 / (PSITR[i] + 0.5));
                    if (KOD0[i] == 7) NVAL[i] = (aa - 1) * (1.98 - 0.58 / PSITR[i]) / (0.93 - 12 * (0.9 - DEL0[i]) * (0.9 - DEL0[i]) + ((0.648 / DEL0[i]) - 0.56) * A[i]);
                    break;
                case 6:
                    aa = KVIT[i];
                    NVAL[i] = (aa * aa - 1) * (0.152 + 1.36 / A[i]) * (2.48 - 2.56 / (PSITR[i] + 1));
                    if (KOD0[i] == 6) NVAL[i] = aa * aa * (aa - 1) * (0.045 + 1.2 / (A[i] + 2)) * (1.74 - 1.12 / (PSITR[i] + 0.5));
                    break;
                case 7:
                    aa = KVIT[i];
                    NVAL[i] = (aa - 1) * (1.65 - 0.39 / PSITR[i]) / (DEL0[i] - 0.4 + (0.75 - 0.625 * DEL0[i]) * A[i]);
                    if (KOD0[i] == 3) NVAL[i] = (aa - 1) * (1.6 - 0.36 / PSITR[i]) / (1.6 * DEL0[i] - 1.11 + (0.674 - 0.54 * DEL0[i]) * A[i]);
                    break;
                case 10:
                    aa = KVIT[i];
                    NVAL[i] = aa * aa * (aa - 1) * (0.01 + 2.3 / (A[i] + 5)) * (1.2 - 0.16 / PSITR[i]);
                    if (KOD0[i] == 6) NVAL[i] = aa * aa * (aa - 1) * (0.045 + 1.2 / (A[i] + 2)) * (1.74 - 1.12 / (PSITR[i] + 0.5));
                    break;
            }
            return NVAL[i];
        }

        /// <summary>
        /// Расчет загрузки электродвигателей. Обрабатывает как индивидуальные, так и групповые привода 
        /// (накопление приведенных моментов для клетей, сидящих на одном валу/двигателе).
        /// </summary>
        public void dvigat(double[] MI, double[] MISUMLocal, double[] KDVPLocal)
        {
            int j = 1; int p;
            PP[NPR + 1] = 1;
            for (int i = 1; i <= NPR; i++)
            {
                p = (int)(PP[i] + 0.05);
                if (p == 1)
                {
                    MISUMLocal[j] = MI[i];
                    NDVR[j] = NR[i] * IR[i];
                    if (NDVR[j] <= NDVN[i]) MDV[j] = 9.549 * NNOM[i] / NDVN[i]; else MDV[j] = 9.549 * NNOM[i] / NDVR[j];
                    KDVPLocal[j] = MISUMLocal[j] / MDV[j];
                    JJ[j] = j; j++;
                }
                else
                {
                    if (p > 1) MISUMLocal[j] = MI[i]; else MISUMLocal[j] += MI[i];
                    if (((int)PP[i + 1]) > 0)
                    {
                        NDVR[j] = NR[i] * IR[i];
                        if (NDVR[j] <= NDVN[i]) MDV[j] = 9.549 * NNOM[i] / NDVN[i]; else MDV[j] = 9.549 * NNOM[i] / NDVR[j];
                        KDVPLocal[j] = MISUMLocal[j] / MDV[j];
                        JJ[j] = j; j++;
                    }
                }
            }
        }

        /// <summary>
        /// Расчет энергосиловых параметров по методу ОМД: контактное давление, усилие прокатки, 
        /// реакции на шейки валков, крутящие моменты, мощность и удельные затраты энергии.
        /// </summary>
        public void power_OMD()
        {
            double R1P = 0, R1Z = 0, R2P = 0, R2Z = 0;
            for (int i = 1; i <= NPR; i++)
            {
                PSRP[i] = 1.15 * SIGSP[i] * nsigma(i);
                PSRZ[i] = 1.15 * SIGSZ[i] * nsigma(i);
                P1P[i] = PSRP[i] * kontakt(i) / 1000;
                P1Z[i] = PSRZ[i] * kontakt(i) / 1000;
                P1P[i] *= Z[i]; P1Z[i] *= Z[i];
                R1P = P1P[i] * SUMX[i] / AKL[i];
                R2P = P1P[i] - R1P;
                RP[i] = Math.Max(R1P, R2P);
                R1Z = P1Z[i] * SUMX[i] / AKL[i];
                R2Z = P1Z[i] - R1Z;
                RZ[i] = Math.Max(R1Z, R2Z);
                KPP[i] = RP[i] / PDOP[i]; KPZ[i] = RZ[i] / PDOP[i];
                MTRP[i] = P1P[i] * FPOD[i] * DSH[i] / 1000;
                MTRZ[i] = P1Z[i] * FPOD[i] * DSH[i] / 1000;
                MDP[i] = 0.287 * SIGSP[i] * H1[i] * H1[i] * H1[i] * A[i] * A[i] * nvalkov(i) * Z[i] / 1000000;
                MDZ[i] = 0.287 * SIGSZ[i] * H1[i] * H1[i] * H1[i] * A[i] * A[i] * nvalkov(i) * Z[i] / 1000000;
                PLECHO[i] = (MDP[i] + MDZ[i]) / (2 * (P1P[i] + P1Z[i]) * LOD[i] / 1000);
                if (PLECHO[i] < 0.35) { MDP[i] = 2 * P1P[i] * LOD[i] * 0.35 / 1000; MDZ[i] = 2 * P1Z[i] * LOD[i] * 0.35 / 1000; }
                if (PLECHO[i] > 0.75) { MDP[i] = 2 * P1P[i] * LOD[i] * 0.75 / 1000; MDZ[i] = 2 * P1Z[i] * LOD[i] * 0.75 / 1000; }
                MPRP[i] = MDP[i] + MTRP[i]; MPRZ[i] = MDZ[i] + MTRZ[i];
                KMP[i] = MPRP[i] / MDOP[i]; KMZ[i] = MPRZ[i] / MDOP[i];
                NPRP[i] = MPRP[i] * NR[i] / 9.549; NPRZ[i] = MPRZ[i] * NR[i] / 9.549;
                WWP[i] = 0.131 * 1000000 * (MPRP[i] / 9.807) * NR[i] * TM[i] / (W[i] * LP[i]);
                WWZ[i] = 0.131 * 1000000 * (MPRZ[i] / 9.807) * NR[i] * TM[i] / (W[i] * LP[i]);
                WWP[i] = WWP[i] / 3600; WWZ[i] = WWZ[i] / 3600;
                MIP[i] = MPRP[i] / (IR[i] * ETA[i]); MIZ[i] = MPRZ[i] / (IR[i] * ETA[i]);
            }
            dvigat(MIP, MISUMP, KDVP);
            dvigat(MIZ, MISUMZ, KDVZ);
        }

        /// <summary>
        /// Оптимизация скоростного режима. Определяет максимально возможную скорость прокатки, 
        /// ограниченную максимальной частотой вращения валков/двигателей (прямой и обратный ход).
        /// </summary>
        public void skorost_max()
        {
            VRMAX[NPR] = VMAX[NPR];
            VRMAX[1] = VRMAX[NPR] * KVIT[1] / (W0 / W[NPR]);
            if (VRMAX[1] > VMAX[1]) { VRMAX[1] = VMAX[1]; VRMAX[NPR] = VRMAX[1] * (W0 / W[NPR]) / KVIT[1]; }
            for (int i = 1; i < NPR - 1; i++)
            {
                VRMAX[i + 1] = VRMAX[i] * KVIT[i + 1];
                if (VRMAX[i + 1] > VMAX[i + 1]) { VRMAX[i + 1] = VMAX[i + 1]; VRMAX[NPR] = VRMAX[i + 1] * W[i + 1] / W[NPR]; }
            }
            for (int i = NPR - 1; i >= 1; i--) VRMAX[i] = VRMAX[i + 1] / KVIT[i + 1];
        }

        /// <summary>
        /// Финальная корректировка степени заполнения калибров перед формированием выходных отчетов.
        /// </summary>
        public void lastcorrection()
        {
            for (int i = 1; i <= NPR; i++)
                if (K17 == 1) DEL1[i] = DELRZ[i];
                else if (DELDOP[i] > 0.1) DEL1[i] = DELDOP[i];
        }

        /// <summary>
        /// Расчет энергосиловых параметров по методу соответственной полосы (Врацкого-Головина).
        /// Приводит фасонный калибр к эквивалентному прямоугольному сечению в гладких валках.
        /// </summary>
        public void sp_polosa()
        {
            for (int i = 1; i <= NPR; i++)
            {
                int metod = (int)(SP[i] + 0.01);
                if (metod == 1)
                {
                    B1SP[i] = B1[i]; H1SP[i] = W[i] / B1SP[i]; B0SP[i] = B0[i];
                    if (i == 1) H0SP[i] = W0 / B0SP[i]; else H0SP[i] = W[i - 1] / B0SP[i];
                }
                else
                {
                    H1SP[i] = Math.Sqrt(W[i] / A1[i]); B1SP[i] = H1SP[i] * A1[i];
                    if (i == 1) H0SP[i] = Math.Sqrt(W0 / (B0[i] / H0[i])); else H0SP[i] = Math.Sqrt(W[i - 1]) / (B0[i] / H0[i]);
                    B0SP[i] = H0SP[i] * (B0[i] / H0[i]);
                }
                ESP[i] = (H0SP[i] - H1SP[i]) / H0SP[i];
                KSISP[i] = 0.105 * NR[i] * Math.Sqrt(ESP[i] * (DB[i] + S[i] - H1SP[i]) / (2 * H0SP[i]));

                if (K11 == 1) { SIGSSPP[i] = sig1(K1, K2, K3, K4, ESP[i], KSISP[i] > 120 ? 120 : KSISP[i], T1P[i]); SIGSSPZ[i] = sig1(K1, K2, K3, K4, ESP[i], KSISP[i] > 120 ? 120 : KSISP[i], T1Z[i]); }
                if (K11 == 2) { SIGSSPP[i] = sig2(K5, K6, K7, K8, K9, ESP[i], KSISP[i] > 120 ? 120 : KSISP[i], T1P[i]); SIGSSPZ[i] = sig2(K5, K6, K7, K8, K9, ESP[i], KSISP[i] > 120 ? 120 : KSISP[i], T1Z[i]); }
                if (K11 == 3) { SIGSSPP[i] = sig3(K1, K2, K3, K4, K5, ESP[i], KSISP[i] > 120 ? 120 : KSISP[i], T1P[i]); SIGSSPZ[i] = sig3(K1, K2, K3, K4, K5, ESP[i], KSISP[i] > 120 ? 120 : KSISP[i], T1Z[i]); }
                SIGSSPP[i] *= 9.807; SIGSSPZ[i] *= 9.807;

                MUSP[i] = 0.55 - 0.00024 * (T1P[i] + T1Z[i]) / 2;
                LSP[i] = Math.Sqrt((DB[i] + S[i] - H1SP[i]) * (H0SP[i] - H1SP[i]) / 2);
                DELSP[i] = 2 * MUSP[i] * LSP[i] / (H0SP[i] - H1SP[i]);
                NSIGSP[i] = 1 + DELSP[i] * (1 - Math.Sqrt(1 - ESP[i])) * (1 - Math.Sqrt(1 - ESP[i])) / ESP[i];
                if (NSIGSP[i] < 1) NSIGSP[i] = 1;
                HSRSP[i] = (H0SP[i] + H1SP[i]) / 2;
                LHSR[i] = LSP[i] / HSRSP[i];
                if (LHSR[i] < 1) NGSP[i] = 2 - Math.Sqrt(LHSR[i]); else NGSP[i] = 1;

                PSRSPP[i] = 1.08 * NSIGSP[i] * NGSP[i] * SIGSSPP[i]; PSRSPZ[i] = 1.08 * NSIGSP[i] * NGSP[i] * SIGSSPZ[i];
                FKONSP[i] = (B0SP[i] + B1SP[i]) * LSP[i] / 2;
                P1SPP[i] = PSRSPP[i] * FKONSP[i] / 1000 * Z[i]; P1SPZ[i] = PSRSPZ[i] * FKONSP[i] / 1000 * Z[i];
                RSPP[i] = Math.Max(P1SPP[i] * SUMX[i] / AKL[i], P1SPP[i] - P1SPP[i] * SUMX[i] / AKL[i]);
                RSPZ[i] = Math.Max(P1SPZ[i] * SUMX[i] / AKL[i], P1SPZ[i] - P1SPZ[i] * SUMX[i] / AKL[i]);
                KPSPP[i] = RSPP[i] / PDOP[i]; KPSPZ[i] = RSPZ[i] / PDOP[i];
                MTRSPP[i] = P1SPP[i] * FPOD[i] * DSH[i] / 1000; MTRSPZ[i] = P1SPZ[i] * FPOD[i] * DSH[i] / 1000;
                MDSPP[i] = 2 * P1SPP[i] * LSP[i] * PSI[i] / 1000; MDSPZ[i] = 2 * P1SPZ[i] * LSP[i] * PSI[i] / 1000;
                MPRSPP[i] = MDSPP[i] + MTRSPP[i]; MPRSPZ[i] = MDSPZ[i] + MTRSPZ[i];
                KMSPP[i] = MPRSPP[i] / MDOP[i]; KMSPZ[i] = MPRSPZ[i] / MDOP[i];
                NPSPP[i] = MPRSPP[i] * NR[i] / 9.549; NPSPZ[i] = MPRSPZ[i] * NR[i] / 9.549;
                WWSPP[i] = 0.131 * 1000000 * (MPRSPP[i] / 9.807) * NR[i] * TM[i] / (W[i] * LP[i]) / 3600;
                WWSPZ[i] = 0.131 * 1000000 * (MPRSPZ[i] / 9.807) * NR[i] * TM[i] / (W[i] * LP[i]) / 3600;
                MISPP[i] = MPRSPP[i] / (IR[i] * ETA[i]); MISPZ[i] = MPRSPZ[i] / (IR[i] * ETA[i]);
            }
            dvigat(MISPP, MISUMSPP, KDVSPP);
            dvigat(MISPZ, MISUMSPZ, KDVSPZ);
        }

        /// <summary>
        /// Итерационный расчет формоизменения (ширины полосы) при неизвестных входящих размерах (K17=1). 
        /// Использует метод последовательных приближений до сходимости результата (точность 0.5%).
        /// </summary>
        public void forma(int i, int sxema)
        {
            KOBJ[i] = H0[i] / H1[i];
            A[i] = DD[i] / H1[i];
            if (BK[i] < 0.001) AK[i] = 1; else AK[i] = BK[i] / HG[i];
            A0[i] = H0[i] / B0[i];
            if (KOD1[i] == 8) TANFI[i] = (BK[i] - BD[i]) / H1[i];
            DEL0[i] = B1[i - 1] / BK[i - 1];
            if (DEL0[i] > 1) DEL0[i] = 1;
            E[i] = stepdef(i, sxema);

            if (i == 1)
            {
                double V0 = VK / (W0 / W[NPR]);
                TP[i] = TAU + L0 / (2 * V0);
            }
            else
            {
                double V0 = V1[i - 1];
                TP[i] = LKL[i - 1] / V0;
            }

            if (K13 == 9) TSR[i] = TOP[i];
            else
            {
                if (i == 1) TSR[i] = temp(P0, TP[i], W0, 0, T0);
                else
                {
                    DTDSR[i - 1] = 0.183 * SIGSSR[i - 1] * Math.Log(KVIT[i - 1]);
                    TSR[i] = temp(PER[i - 1], TP[i], W[i - 1], DTDSR[i - 1], TSR[i - 1]);
                }
            }

            if (SXEMA[i] == 37 || SXEMA[i] == 73 || SXEMA[i] == 77)
            {
                PSITR[i] = 0.5; if (TSR[i] < 1100) PSITR[i] = 0.6; if (TSR[i] < 1000) PSITR[i] = 0.75;
            }
            else
            {
                PSITR[i] = 0.6; if (TSR[i] < 1200) PSITR[i] = 0.7; if (TSR[i] < 1100) PSITR[i] = 0.8; if (TSR[i] < 1000) PSITR[i] = 0.9;
                if (KOD1[i] == 8 || KOD1[i] == 1 || KOD1[i] == 0) PSITR[i] -= 0.1;
            }
            if (TSR[i] < 900) PSITR[i] = 1;

            switch (KOD1[i])
            {
                case 0:
                case 1:
                    if (KOD0[i] == 2) BETAR[i] = spred(0.179, 1.357, 0.291, 0, 0, 0, 0.511, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    if (KOD0[i] == 1) BETAR[i] = spred(0.0714, 0.862, 0.555, 0.763, 0, 0, 0.455, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    break;
                case 2:
                    if (KOD0[i] == 4) BETAR[i] = spred(0.386, 1.163, 0.402, -2.171, 0, -1.324, 0.616, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    if (KOD0[i] == 5) BETAR[i] = spred(0.693, 1.286, 0.368, -1.052, 0, -2.231, 0.629, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    break;
                case 3:
                    if (KOD0[i] == 4) BETAR[i] = spred(2.242, 1.151, 0.352, -2.234, 0, -1.647, 1.137, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    if (KOD0[i] == 6) BETAR[i] = spred(0.360, 0.658, 0.202, -0.467, 0, -3.316, 0.494, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    if (KOD0[i] == 7) BETAR[i] = spred(0.972, 2.01, 0.665, -2.458, 0, -1.3, 0.7, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    break;
                case 4:
                    if (KOD0[i] == 1 || KOD0[i] == 8) BETAR[i] = spred(0.377, 0.507, 0.316, 0, -0.405, 0, 1.136, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    if (KOD0[i] == 2) BETAR[i] = spred(0.227, 1.563, 0.591, 0, -0.852, 0, 0.587, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    if (KOD0[i] == 4) BETAR[i] = spred(0.405, 1.163, 0.403, -2.171, -0.789, -1.324, 0.616, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    if (KOD0[i] == 9) BETAR[i] = spred(1.623, 2.272, 0.761, -0.582, -3.064, 0, 0.486, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    break;
                case 5:
                    if (KOD0[i] == 1) BETAR[i] = spred(0.134, 0.717, 0.474, 0, -0.507, 0, 0.357, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    break;
                case 6:
                    if (KOD0[i] == 1) BETAR[i] = spred(2.075, 1.848, 0.815, 0, -3.453, 0, 0.659, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    break;
                case 7:
                    if (KOD0[i] == 3) BETAR[i] = spred(3.09, 2.07, 0.5, 0, -4.85, -4.865, 1.543, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    if (KOD0[i] == 7) BETAR[i] = spred(0.506, 1.876, 0.895, -2.22, -2.22, -2.73, 0.587, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    break;
                case 8:
                    if (KOD0[i] == 8 || KOD0[i] == 1) BETAR[i] = spred(0.0714, 0.862, 0.746, 0.763, 0, 0, 0.16, 0.362, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    break;
                case 9:
                    if (KOD0[i] == 4) BETAR[i] = spred(0.575, 1.163, 0.402, -2.171, -4.265, -1.324, 0.616, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    break;
                case 10:
                    if (KOD0[i] == 6) BETAR[i] = spred(0.3, 1.203, 0.368, -0.852, 0, -3.45, 0.629, 0, KOBJ[i], A[i], A0[i], AK[i], DEL0[i], PSITR[i], TANFI[i]);
                    break;
            }

            B1[i] = B0[i] * BETAR[i];
            double b;
            do
            {
                double sb = 0;
                b = B1[i];
                A1[i] = b / H1[i];
                DEL1[i] = b / BK[i];
                if (DEL1[i] > 1) DEL1[i] = 1;
                W[i] = omega(i);
                if (i == 1) KVIT[i] = W0 / W[i]; else KVIT[i] = W[i - 1] / W[i];

                if (i == 1) V1[i] = VK * KVIT[i] / (W0 / W[NPR]); else V1[i] = V1[i - 1] * KVIT[i];
                DK[i] = (DB[i] + S[i]) - W[i] / b;
                NR[i] = 60000 * V1[i] / (PI * DK[i]);
                if (K12 == 1) { NR[i] = NZ[i]; V1[i] = PI * DK[i] * NR[i] / 60000; }
                KSI[i] = 0.105 * NR[i] * Math.Sqrt(E[i] * DK[i] / (2 * H0[i]));

                if (K11 == 1) SIGSSR[i] = 9.807 * sig1(K1, K2, K3, K4, E[i], KSI[i] > 120 ? 120 : KSI[i], TSR[i]);
                if (K11 == 2) SIGSSR[i] = 9.807 * sig2(K5, K6, K7, K8, K9, E[i], KSI[i] > 120 ? 120 : KSI[i], TSR[i]);
                if (K11 == 3) SIGSSR[i] = 9.807 * sig3(K1, K2, K3, K4, K5, E[i], KSI[i] > 120 ? 120 : KSI[i], TSR[i]);

                if (K11 == 1 || K11 == 3) sb = 9.807 * sig1(130, 0.252, 0.143, 0.0025, E[i], KSI[i] > 120 ? 120 : KSI[i], TSR[i]);
                if (K11 == 2) sb = 9.807 * sig2(1.41, 9.07, 0.124, 0.167, -2.54, E[i], KSI[i] > 120 ? 120 : KSI[i], TSR[i]);

                if ((SIGSSR[i] / sb) > 1.001) KBETAP[i] = 1 + 0.6 * (Math.Exp(0.544 * Math.Log((SIGSSR[i] / sb) - 1)));
                else KBETAP[i] = 1;

                BETASTP[i] = 1 + (BETAR[i] - 1) * KBETAP[i];
                B1[i] = B0[i] * BETASTP[i];

                switch (KOD1[i])
                {
                    case 0: case 1: PER[i] = 2 * (H1[i] + B1[i]); break;
                    case 2: PER[i] = PI * H1[i]; break;
                    case 3: PER[i] = 2.828 * BK[i]; break;
                    case 4: PER[i] = 2 * Math.Sqrt(B1[i] * B1[i] + 4 * (H1[i] * H1[i]) / 3); break;
                    case 5: PER[i] = PI * H1[i] + 2 * (B1[i] - H1[i]); break;
                    case 6: case 8: if (KOD0[i] == 6) PER[i] = 3 * H1[i]; else PER[i] = 2 * (BD[i] + H1[i]) / Math.Cos(Math.Atan((BVR[i] - BD[i]) / (H1[i] - S[i]))); break;
                    case 7: PER[i] = 2 * Math.Sqrt(H1[i] * H1[i] + B1[i] * B1[i]); break;
                    case 9: PER[i] = 2 * Math.Sqrt(H1[i] * H1[i] + 4 * B1[i] * B1[i] / 3); break;
                }
            } while ((Math.Abs(b - B1[i]) / b) > 0.005);

            peresilka(i);
        }

        /// <summary>
        /// Передача рассчитанных размеров (H1, B1) текущего прохода в качестве входящих (H0, B0) 
        /// для следующего прохода с учетом логики кантовки (поворота раската на 45 или 90 градусов).
        /// </summary>
        public void peresilka(int i)
        {
            switch (KOD1[i])
            {
                case 2: H0[i + 1] = H1[i]; B0[i + 1] = B1[i]; break;
                case 4: case 5: case 6: case 7: case 9: H0[i + 1] = B1[i]; B0[i + 1] = H1[i]; break;
                case 3:
                    if (SXEMA[i + 1] == 37) { H0[i + 1] = H1[i]; B0[i + 1] = B1[i]; }
                    else { double c = (H1[i] + 0.83 * R[i]) / Math.Sqrt(2); H0[i + 1] = c; B0[i + 1] = c; }
                    break;
                case 8:
                case 0:
                    if (H1[i] < H1[i + 1]) { H0[i + 1] = B1[i]; B0[i + 1] = H1[i]; }
                    else { H0[i + 1] = H1[i]; B0[i + 1] = B1[i]; }
                    break;
            }
        }

        /// <summary>
        /// Расчет установочного зазора валков (нажимного механизма) с учетом упругой деформации клети 
        /// (растяжение станин, сжатие подушек и валков).
        /// </summary>
        public void Zazor()
        {
            for (int i = 1; i <= NPR; i++)
            {
                if (C[i] > 0.001) // Если задана жесткость клети
                {
                    FKLP[i] = P1P[i] / C[i]; // упругая деформация передний конец
                    FKLZ[i] = P1Z[i] / C[i]; // упругая деформация задний конец
                    FKL[i] = (FKLP[i] + FKLZ[i]) / 2; // среднее значение

                    SUP[i] = H1[i] - 2 * HVR[i] - FKLP[i];
                    SUZ[i] = H1[i] - 2 * HVR[i] - FKLZ[i];
                    SU[i] = H1[i] - 2 * HVR[i] - FKL[i];
                }
            }
        }

        /// <summary>
        /// Финализация данных для UI и отчетов: усреднение параметров переднего и заднего концов раската, 
        /// а также разворачивание массивов частот вращения для корректного отображения групповых приводов.
        /// </summary>
        public void DataFromProgramm()
        {
            for (int i = 1; i <= NPR; i++)
            {
                // Усреднение (Передний + Задний) / 2
                MDS[i] = (MDP[i] + MDZ[i]) / 2;
                MISUMS[i] = (MISUMP[i] + MISUMZ[i]) / 2;
                MTRS[i] = (MTRP[i] + MTRZ[i]) / 2;
                MPRS[i] = (MPRP[i] + MPRZ[i]) / 2;
                KMS[i] = (KMP[i] + KMZ[i]) / 2;
                WWS[i] = (WWP[i] + WWZ[i]) / 2;
                PSRS[i] = (PSRP[i] + PSRZ[i]) / 2;
                P1S[i] = (P1P[i] + P1Z[i]) / 2;
                RS[i] = (RP[i] + RZ[i]) / 2;
                KPS[i] = (KPP[i] + KPZ[i]) / 2;
                NPRS[i] = (NPRP[i] + NPRZ[i]) / 2;
                KDVS[i] = (KDVP[i] + KDVZ[i]) / 2;

                // Защита от нулей (если считалось по методу соответственной полосы)
                if (Math.Abs(MDS[i]) < 0.001) MDS[i] = (MDSPP[i] + MDSPZ[i]) / 2;
                if (Math.Abs(MISUMS[i]) < 0.001) MISUMS[i] = (MISUMSPP[i] + MISUMSPZ[i]) / 2;
                if (Math.Abs(MTRS[i]) < 0.001) MTRS[i] = (MTRSPP[i] + MTRSPZ[i]) / 2;
                if (Math.Abs(MPRS[i]) < 0.001) MPRS[i] = (MPRSPP[i] + MPRSPZ[i]) / 2;
                if (Math.Abs(KMS[i]) < 0.001) KMS[i] = (KMSPP[i] + KMSPZ[i]) / 2;
                if (Math.Abs(WWS[i]) < 0.001) WWS[i] = (WWSPP[i] + WWSPZ[i]) / 2;
                if (Math.Abs(PSRS[i]) < 0.001) PSRS[i] = (PSRSPP[i] + PSRSPZ[i]) / 2;
                if (Math.Abs(P1S[i]) < 0.001) P1S[i] = (P1SPP[i] + P1SPZ[i]) / 2;
                if (Math.Abs(RS[i]) < 0.001) RS[i] = (RSPP[i] + RSPZ[i]) / 2;
                if (Math.Abs(KPS[i]) < 0.001) KPS[i] = (KPSPP[i] + KPSPZ[i]) / 2;
                if (Math.Abs(NPRS[i]) < 0.001) NPRS[i] = (NPSPP[i] + NPSPZ[i]) / 2;
                if (Math.Abs(KDVS[i]) < 0.001) KDVS[i] = (KDVSPP[i] + KDVSPZ[i]) / 2;

                if (Math.Abs(FKON[i]) < 0.001) FKON[i] = FKONSP[i];
            }

            // Восстановление логики групповых приводов
            int Do_Group = 0;
            for (int i = 1; (i <= NPR) && (NDVR[i] > 0.01); i++)
            {
                NDVR_[i + Do_Group] = NDVR[i];
                if (PP[i] > 1) // Если привод групповой
                {
                    for (int i1 = i; i1 < i + (int)PP[i]; i1++)
                    {
                        NDVR_[i1 + Do_Group] = NDVR[i + Do_Group];
                    }
                    Do_Group = Do_Group + (int)PP[i] - 1;
                }
            }
        }
    }
}