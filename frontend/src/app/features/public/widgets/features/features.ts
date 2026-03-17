import { Component } from '@angular/core';

interface FeatureItem {
  title: string;
  description: string;
  icon: string;
  iconColor: string;
  bgColor: string;
  gradient: string;
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
      title: 'Easy to Use',
      description: 'Posuere morbi leo urna molestie.',
      icon: 'pi-users',
      iconColor: 'text-yellow-700',
      bgColor: 'bg-yellow-200',
      gradient:
        'background: linear-gradient(90deg, rgba(253, 228, 165, 0.2), rgba(187, 199, 205, 0.2)), linear-gradient(180deg, rgba(253, 228, 165, 0.2), rgba(187, 199, 205, 0.2))'
    },
    {
      title: 'Fresh Design',
      description: 'Semper risus in hendrerit.',
      icon: 'pi-palette',
      iconColor: 'text-cyan-700',
      bgColor: 'bg-cyan-200',
      gradient:
        'background: linear-gradient(90deg, rgba(145, 226, 237, 0.2), rgba(251, 199, 145, 0.2)), linear-gradient(180deg, rgba(253, 228, 165, 0.2), rgba(172, 180, 223, 0.2))'
    },
    {
      title: 'Well Documented',
      description: 'Non arcu risus quis varius quam quisque.',
      icon: 'pi-map',
      iconColor: 'text-indigo-700',
      bgColor: 'bg-indigo-200',
      gradient:
        'background: linear-gradient(90deg, rgba(145, 226, 237, 0.2), rgba(172, 180, 223, 0.2)), linear-gradient(180deg, rgba(172, 180, 223, 0.2), rgba(246, 158, 188, 0.2))'
    },
    {
      title: 'Responsive Layout',
      description: 'Nulla malesuada pellentesque elit.',
      icon: 'pi-id-card',
      iconColor: 'text-surface-700',
      bgColor: 'bg-surface-200',
      gradient:
        'background: linear-gradient(90deg, rgba(187, 199, 205, 0.2), rgba(251, 199, 145, 0.2)), linear-gradient(180deg, rgba(253, 228, 165, 0.2), rgba(145, 210, 204, 0.2))'
    },
    {
      title: 'Clean Code',
      description: 'Condimentum lacinia quis vel eros.',
      icon: 'pi-star',
      iconColor: 'text-orange-700',
      bgColor: 'bg-orange-200',
      gradient:
        'background: linear-gradient(90deg, rgba(187, 199, 205, 0.2), rgba(246, 158, 188, 0.2)), linear-gradient(180deg, rgba(145, 226, 237, 0.2), rgba(160, 210, 250, 0.2))'
    },
    {
      title: 'Dark Mode',
      description: 'Convallis tellus id interdum velit laoreet.',
      icon: 'pi-moon',
      iconColor: 'text-pink-700',
      bgColor: 'bg-pink-200',
      gradient:
        'background: linear-gradient(90deg, rgba(251, 199, 145, 0.2), rgba(246, 158, 188, 0.2)), linear-gradient(180deg, rgba(172, 180, 223, 0.2), rgba(212, 162, 221, 0.2))'
    },
    {
      title: 'Ready to Use',
      description: 'Mauris sit amet massa vitae.',
      icon: 'pi-shopping-cart',
      iconColor: 'text-teal-700',
      bgColor: 'bg-teal-200',
      gradient:
        'background: linear-gradient(90deg, rgba(145, 210, 204, 0.2), rgba(160, 210, 250, 0.2)), linear-gradient(180deg, rgba(187, 199, 205, 0.2), rgba(145, 210, 204, 0.2))'
    },
    {
      title: 'Modern Practices',
      description: 'Elementum nibh tellus molestie nunc non.',
      icon: 'pi-globe',
      iconColor: 'text-blue-700',
      bgColor: 'bg-blue-200',
      gradient:
        'background: linear-gradient(90deg, rgba(145, 210, 204, 0.2), rgba(212, 162, 221, 0.2)), linear-gradient(180deg, rgba(251, 199, 145, 0.2), rgba(160, 210, 250, 0.2))'
    },
    {
      title: 'Privacy',
      description: 'Neque egestas congue quisque.',
      icon: 'pi-eye',
      iconColor: 'text-purple-700',
      bgColor: 'bg-purple-200',
      gradient:
        'background: linear-gradient(90deg, rgba(160, 210, 250, 0.2), rgba(212, 162, 221, 0.2)), linear-gradient(180deg, rgba(246, 158, 188, 0.2), rgba(212, 162, 221, 0.2))'
    }
  ];
}
