import { Component } from '@angular/core';

interface FeatureItem {
  title: string;
  description: string;
  icon: string;
  iconColor: string;
  bgColor: string;
}

@Component({
  selector: 'app-landing-features',
  standalone: true,
  imports: [],
  templateUrl: './features.html'
})
export class LandingFeatures {
  features: FeatureItem[] = [
    {
      title: '一键接入',
      description: '获取一个 API 密钥，即可调用所有已接入的 AI 模型，无需分别申请。',
      icon: 'pi-server',
      iconColor: 'text-white',
      bgColor: 'bg-primary-600'
    },
    {
      title: '稳定可靠',
      description: '智能调度多个上游账号，自动切换和负载均衡，告别频繁报错。',
      icon: 'pi-users',
      iconColor: 'text-white',
      bgColor: 'bg-primary-500'
    },
    {
      title: '用多少付多少',
      description: '按实际使用量计费，支持设置配额上限，团队用量一目了然。',
      icon: 'pi-wallet',
      iconColor: 'text-white',
      bgColor: 'bg-primary-400'
    }
  ];
}
