import { ModelVendor } from '../../../shared/models/model-vendor.enum';

export type ModelCategory = 'Chat' | 'Image' | 'Video' | 'Audio' | 'Embedding';

export interface ModelOptionOutputDto {
  label: string;
  value: string;
  category?: ModelCategory;
  vendor?: ModelVendor;
}
