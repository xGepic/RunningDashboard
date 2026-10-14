import * as echarts from 'echarts/core';
import { BarChart, LineChart, ScatterChart } from 'echarts/charts';
import { GridComponent, MarkAreaComponent, MarkLineComponent, TooltipComponent } from 'echarts/components';
import { LegacyGridContainLabel } from 'echarts/features';
import { CanvasRenderer } from 'echarts/renderers';

// Only what the charts use, a fraction of the whole of ECharts; a chart with another series type or component must add
// it here. LegacyGridContainLabel keeps the grid's containLabel working, which the whole of ECharts brings along.
echarts.use([
  BarChart,
  LineChart,
  ScatterChart,
  GridComponent,
  TooltipComponent,
  MarkLineComponent,
  MarkAreaComponent,
  LegacyGridContainLabel,
  CanvasRenderer,
]);

export { echarts };
